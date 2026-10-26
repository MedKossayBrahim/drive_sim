// Petit serveur de comptes pour Drive Sim.
// Le téléphone vérifie le visage. Ce serveur vérifie l'e-mail, le mot de passe et les jetons.
// Il ne reçoit jamais de photo ni de modèle biométrique.

import express from "express";
import cors from "cors";
import bcrypt from "bcryptjs";
import jwt from "jsonwebtoken";
import crypto from "node:crypto";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const PORT = 5080;
const JWT_SECRET = process.env.JWT_SECRET || "drive-sim-dev-secret-change-me";
const ACCESS_TOKEN_MINUTES = 15;
const REFRESH_DAYS = 30;
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dataDir = path.join(__dirname, "data");
const dataFile = path.join(dataDir, "users.json");

fs.mkdirSync(dataDir, { recursive: true });

function loadDb() {
  if (!fs.existsSync(dataFile)) {
    return { users: [] };
  }
  return JSON.parse(fs.readFileSync(dataFile, "utf8"));
}

function saveDb(db) {
  fs.writeFileSync(dataFile, JSON.stringify(db, null, 2));
}

function hashToken(token) {
  return crypto.createHash("sha256").update(token).digest("hex");
}

function publicUser(user) {
  return { id: user.id, email: user.email, displayName: user.displayName };
}

function normalizeEmail(email) {
  return String(email || "").trim().toLowerCase();
}

function validateCredentials(email, password, displayName) {
  if (!email.includes("@") || !email.includes(".") || email.length > 120) {
    return "Entrez un e-mail valide.";
  }
  if (typeof password !== "string" || password.length < 6 || password.length > 72) {
    return "Le mot de passe doit contenir entre 6 et 72 caractères.";
  }
  if (displayName !== undefined) {
    const name = String(displayName).trim();
    if (name.length < 2 || name.length > 24) {
      return "Le pseudo doit contenir entre 2 et 24 caractères.";
    }
  }
  return null;
}

function issueTokens(user) {
  const accessToken = jwt.sign(
    { sub: user.id, email: user.email, name: user.displayName },
    JWT_SECRET,
    { expiresIn: `${ACCESS_TOKEN_MINUTES}m` }
  );
  const refreshToken = crypto.randomBytes(32).toString("hex");
  const now = Date.now();
  user.refreshTokens = (user.refreshTokens || [])
    .filter((item) => item.expiresAt > now)
    .slice(-7);
  user.refreshTokens.push({
    hash: hashToken(refreshToken),
    expiresAt: now + REFRESH_DAYS * 24 * 60 * 60 * 1000,
  });
  return { accessToken, refreshToken, user: publicUser(user) };
}

function readBearer(request) {
  const header = request.get("authorization") || "";
  const [scheme, token] = header.split(" ");
  if (scheme !== "Bearer" || !token) return null;
  return token;
}

const app = express();
app.use(cors());
app.use(express.json({ limit: "32kb" }));
app.use((request, response, next) => {
  response.on("finish", () => {
    console.log(`${request.method} ${request.path} -> ${response.statusCode}`);
  });
  next();
});

app.get("/health", (_request, response) => {
  response.json({ ok: true });
});

app.post("/auth/register", async (request, response) => {
  const email = normalizeEmail(request.body.email);
  const password = request.body.password;
  const displayName = String(request.body.displayName || "").trim();
  const problem = validateCredentials(email, password, displayName);
  if (problem) return response.status(400).json({ error: problem });

  const db = loadDb();
  if (db.users.some((user) => user.email === email)) {
    return response.status(409).json({ error: "Un compte existe déjà avec cet e-mail." });
  }

  const user = {
    id: crypto.randomUUID(),
    email,
    displayName,
    passwordHash: await bcrypt.hash(password, 10),
    refreshTokens: [],
  };
  const tokens = issueTokens(user);
  db.users.push(user);
  saveDb(db);
  response.status(201).json(tokens);
});

app.post("/auth/login", async (request, response) => {
  const email = normalizeEmail(request.body.email);
  const password = request.body.password || "";
  const db = loadDb();
  const user = db.users.find((item) => item.email === email);
  const matches = user ? await bcrypt.compare(password, user.passwordHash) : false;
  if (!matches) {
    return response.status(401).json({ error: "E-mail ou mot de passe incorrect." });
  }
  const tokens = issueTokens(user);
  saveDb(db);
  response.json(tokens);
});

app.post("/auth/refresh", (request, response) => {
  const refreshToken = String(request.body.refreshToken || "");
  if (!refreshToken) return response.status(401).json({ error: "Session absente." });

  const db = loadDb();
  const hashed = hashToken(refreshToken);
  const now = Date.now();
  let stored = null;
  const user = db.users.find((item) => {
    stored = (item.refreshTokens || []).find((token) => token.hash === hashed && token.expiresAt > now);
    return Boolean(stored);
  });
  if (!user) {
    return response.status(401).json({ error: "La session a expiré. Connectez-vous avec le mot de passe." });
  }

  // On garde le même jeton : il est déjà enfermé par la biométrie du téléphone.
  stored.expiresAt = now + REFRESH_DAYS * 24 * 60 * 60 * 1000;
  const accessToken = jwt.sign(
    { sub: user.id, email: user.email, name: user.displayName },
    JWT_SECRET,
    { expiresIn: `${ACCESS_TOKEN_MINUTES}m` }
  );
  saveDb(db);
  response.json({ accessToken, refreshToken, user: publicUser(user) });
});

app.post("/auth/logout", (request, response) => {
  const refreshToken = String(request.body.refreshToken || "");
  if (refreshToken) {
    const db = loadDb();
    const hashed = hashToken(refreshToken);
    const user = db.users.find((item) =>
      (item.refreshTokens || []).some((token) => token.hash === hashed)
    );
    if (user) user.refreshTokens = [];
    saveDb(db);
  }
  response.json({ ok: true });
});

// --- Score / Leaderboard ---

app.post("/scores", (request, response) => {
  const token = readBearer(request);
  if (!token) return response.status(401).json({ error: "Connexion requise." });
  try {
    const payload = jwt.verify(token, JWT_SECRET);
    const db = loadDb();
    const user = db.users.find((item) => item.id === payload.sub);
    if (!user) return response.status(401).json({ error: "Compte introuvable." });

    const score = Number(request.body.score) || 0;
    const depth = Number(request.body.maxDepth) || 0;
    const time = Number(request.body.playTime) || 0;
    const cores = Number(request.body.cores) || 0;

    if (!db.scores) db.scores = [];
    db.scores.push({
      userId: user.id,
      displayName: user.displayName,
      score,
      maxDepth: depth,
      playTime: time,
      cores,
      createdAt: Date.now(),
    });

    // Keep top 50
    db.scores.sort((a, b) => b.score - a.score);
    db.scores = db.scores.slice(0, 50);
    saveDb(db);

    response.status(201).json({ ok: true, rank: db.scores.findIndex((s) => s.userId === user.id && s.score === score) + 1 });
  } catch {
    response.status(401).json({ error: "Session invalide. Reconnectez-vous." });
  }
});

app.get("/scores", (_request, response) => {
  const db = loadDb();
  const scores = (db.scores || []).slice(0, 20).map((s) => ({
    displayName: s.displayName,
    score: s.score,
    maxDepth: s.maxDepth,
    playTime: s.playTime,
    cores: s.cores,
    createdAt: s.createdAt,
  }));
  response.json({ scores });
});

app.get("/users/me", (request, response) => {
  const token = readBearer(request);
  if (!token) return response.status(401).json({ error: "Connexion requise." });
  try {
    const payload = jwt.verify(token, JWT_SECRET);
    const db = loadDb();
    const user = db.users.find((item) => item.id === payload.sub);
    if (!user) return response.status(401).json({ error: "Compte introuvable." });
    response.json({ user: publicUser(user) });
  } catch {
    response.status(401).json({ error: "Session invalide. Reconnectez-vous." });
  }
});

const server = app.listen(PORT, "0.0.0.0", () => {
  const ips = [];
  try {
    for (const entries of Object.values(os.networkInterfaces())) {
      for (const entry of entries || []) {
        if (entry.family === "IPv4" && !entry.internal) ips.push(entry.address);
      }
    }
  } catch (error) {
    console.log("Adresses Wi-Fi illisibles pour le moment (" + error.message + ").");
    console.log("Sur le Mac : Réglages Système > Réseau, et note l'adresse IP.");
  }
  console.log("");
  console.log("Serveur Drive Sim prêt.");
  console.log(`  Sur cet ordinateur : http://localhost:${PORT}`);
  if (ips.length === 0) {
    console.log("  Aucune adresse Wi-Fi affichée. Branche l'ordinateur au Wi-Fi, puis relance npm start.");
  } else {
    for (const ip of ips) {
      console.log(`  Sur le téléphone    : http://${ip}:${PORT}`);
    }
  }
  console.log("Laissez cette fenêtre ouverte pendant les tests.");
  console.log("");
});

server.on("error", (error) => {
  if (error.code === "EADDRINUSE") {
    console.error(`Le port ${PORT} est déjà utilisé. Fermez l'autre serveur, ou changez PORT dans server.js et dans AuthConfig.cs.`);
    process.exit(1);
  }
  console.error(error);
  process.exit(1);
});
