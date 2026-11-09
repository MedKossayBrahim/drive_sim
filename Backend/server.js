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

function normalizeUser(user) {
  if (typeof user.coins !== "number" || !Number.isFinite(user.coins)) user.coins = 0;
  if (typeof user.level !== "number" || !Number.isFinite(user.level)) user.level = 1;
  if (typeof user.score !== "number" || !Number.isFinite(user.score)) user.score = 0;
  if (typeof user.bestPlane !== "number" || !Number.isFinite(user.bestPlane)) user.bestPlane = 0;
  if (typeof user.bestBoat !== "number" || !Number.isFinite(user.bestBoat)) user.bestBoat = 0;
  if (typeof user.bestCar !== "number" || !Number.isFinite(user.bestCar)) user.bestCar = 0;
  if (typeof user.bestBike !== "number" || !Number.isFinite(user.bestBike)) user.bestBike = 0;
  if (typeof user.bestSubmarine !== "number" || !Number.isFinite(user.bestSubmarine)) user.bestSubmarine = 0;
  if (!Array.isArray(user.scoreRuns)) user.scoreRuns = [];
  user.coins = Math.max(0, Math.min(Math.floor(user.coins), 999999999));
  user.level = Math.max(1, Math.min(Math.floor(user.level), 5));
  user.score = Math.max(0, Math.min(Math.floor(user.score), 999999999));
  user.bestPlane = Math.max(0, Math.min(Math.floor(user.bestPlane), 999999999));
  user.bestBoat = Math.max(0, Math.min(Math.floor(user.bestBoat), 999999999));
  user.bestCar = Math.max(0, Math.min(Math.floor(user.bestCar), 999999999));
  user.bestBike = Math.max(0, Math.min(Math.floor(user.bestBike), 999999999));
  user.bestSubmarine = Math.max(0, Math.min(Math.floor(user.bestSubmarine), 999999999));
  user.scoreRuns = user.scoreRuns.slice(-40);
  return user;
}

function publicUser(user) {
  const safe = normalizeUser(user);
  return {
    id: safe.id,
    email: safe.email,
    displayName: safe.displayName,
    coins: safe.coins,
    level: safe.level,
    score: safe.score,
    bestPlane: safe.bestPlane,
    bestBoat: safe.bestBoat,
    bestCar: safe.bestCar,
    bestBike: safe.bestBike,
    bestSubmarine: safe.bestSubmarine,
  };
}

function requireUser(request, response) {
  const token = readBearer(request);
  if (!token) {
    response.status(401).json({ error: "Connexion requise." });
    return null;
  }
  try {
    const payload = jwt.verify(token, JWT_SECRET);
    const db = loadDb();
    const user = db.users.find((item) => item.id === payload.sub);
    if (!user) {
      response.status(401).json({ error: "Compte introuvable." });
      return null;
    }
    return { db, user };
  } catch {
    response.status(401).json({ error: "Session invalide. Reconnectez-vous." });
    return null;
  }
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

  const user = normalizeUser({
    id: crypto.randomUUID(),
    email,
    displayName,
    passwordHash: await bcrypt.hash(password, 10),
    refreshTokens: [],
    coins: 0,
    level: 1,
    score: 0,
    bestPlane: 0,
    bestBoat: 0,
    bestCar: 0,
    bestBike: 0,
    bestSubmarine: 0,
    scoreRuns: [],
  });
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

app.get("/users/me", (request, response) => {
  const auth = requireUser(request, response);
  if (!auth) return;
  normalizeUser(auth.user);
  saveDb(auth.db);
  response.json({ user: publicUser(auth.user) });
});

app.put("/users/progress", (request, response) => {
  const auth = requireUser(request, response);
  if (!auth) return;
  const { db, user } = auth;

  const coins = Number(request.body.coins);
  const level = Number(request.body.level);
  const score = Number(request.body.score);
  const bestPlane = Number(request.body.bestPlane ?? 0);
  const bestBoat = Number(request.body.bestBoat ?? 0);
  const bestCar = Number(request.body.bestCar ?? 0);
  const bestBike = Number(request.body.bestBike ?? 0);
  const bestSubmarine = Number(request.body.bestSubmarine ?? 0);
  if (![coins, level, score, bestPlane, bestBoat, bestCar, bestBike, bestSubmarine].every((value) => Number.isFinite(value))) {
    return response.status(400).json({ error: "coins, level et score doivent être des nombres." });
  }

  normalizeUser(user);
  user.coins = Math.max(user.coins, Math.floor(coins));
  user.level = Math.max(user.level, Math.floor(level));
  user.score = Math.max(
    user.score,
    Math.floor(score),
    Math.floor(bestPlane),
    Math.floor(bestBoat),
    Math.floor(bestCar),
    Math.floor(bestBike),
    Math.floor(bestSubmarine)
  );
  user.bestPlane = Math.max(user.bestPlane, Math.floor(bestPlane));
  user.bestBoat = Math.max(user.bestBoat, Math.floor(bestBoat));
  user.bestCar = Math.max(user.bestCar, Math.floor(bestCar));
  user.bestBike = Math.max(user.bestBike, Math.floor(bestBike));
  user.bestSubmarine = Math.max(user.bestSubmarine, Math.floor(bestSubmarine));
  normalizeUser(user);
  saveDb(db);
  response.json({ user: publicUser(user) });
});

// Scores de course (sous-marin et futurs modes). Met à jour bestSubmarine / score.
app.post("/scores", (request, response) => {
  const auth = requireUser(request, response);
  if (!auth) return;
  const { db, user } = auth;

  const score = Math.floor(Number(request.body.score));
  const maxDepth = Number(request.body.maxDepth);
  const playTime = Number(request.body.playTime);
  const cores = Math.floor(Number(request.body.cores ?? 0));
  if (![score, maxDepth, playTime, cores].every((value) => Number.isFinite(value))) {
    return response.status(400).json({ error: "score, maxDepth, playTime et cores doivent être des nombres." });
  }

  normalizeUser(user);
  const runScore = Math.max(0, score);
  user.bestSubmarine = Math.max(user.bestSubmarine, runScore);
  user.score = Math.max(user.score, runScore);
  user.scoreRuns.push({
    vehicle: "submarine",
    score: runScore,
    maxDepth: Math.max(0, maxDepth),
    playTime: Math.max(0, playTime),
    cores: Math.max(0, cores),
    at: new Date().toISOString(),
  });
  normalizeUser(user);
  saveDb(db);
  response.status(201).json({
    ok: true,
    user: publicUser(user),
    bestSubmarine: user.bestSubmarine,
  });
});

app.get("/scores", (request, response) => {
  const auth = requireUser(request, response);
  if (!auth) return;
  normalizeUser(auth.user);
  const board = auth.db.users
    .map((item) => {
      normalizeUser(item);
      return {
        id: item.id,
        displayName: item.displayName,
        bestSubmarine: item.bestSubmarine,
        score: item.score,
      };
    })
    .filter((item) => item.bestSubmarine > 0 || item.score > 0)
    .sort((a, b) => b.bestSubmarine - a.bestSubmarine || b.score - a.score)
    .slice(0, 20);
  response.json({ scores: board, mine: publicUser(auth.user) });
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
