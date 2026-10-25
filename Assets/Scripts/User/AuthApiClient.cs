using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace DriveSim.User
{
    public sealed class AuthResult
    {
        public bool Ok;
        public bool Unauthorized;
        public string Error;
        public string AccessToken;
        public string RefreshToken;
        public PlayerProfile Player;
    }

    public static class AuthApiClient
    {
        public static async Task<bool> IsReachable(string baseUrl)
        {
            var result = await Send(baseUrl, "/health", "GET", null, null);
            return result.Ok;
        }

        public static Task<AuthResult> Register(string baseUrl, string email, string password, string displayName)
        {
            var body = JsonUtility.ToJson(new RegisterBody
            {
                email = email,
                password = password,
                displayName = displayName
            });
            return Send(baseUrl, "/auth/register", "POST", body, null);
        }

        public static Task<AuthResult> Login(string baseUrl, string email, string password)
        {
            var body = JsonUtility.ToJson(new LoginBody { email = email, password = password });
            return Send(baseUrl, "/auth/login", "POST", body, null);
        }

        public static Task<AuthResult> Refresh(string baseUrl, string refreshToken)
        {
            var body = JsonUtility.ToJson(new RefreshBody { refreshToken = refreshToken });
            return Send(baseUrl, "/auth/refresh", "POST", body, null);
        }

        public static async Task<AuthResult> Logout(string baseUrl, string refreshToken)
        {
            var body = JsonUtility.ToJson(new RefreshBody { refreshToken = refreshToken });
            return await Send(baseUrl, "/auth/logout", "POST", body, null);
        }

        public static Task<AuthResult> Me(string baseUrl, string accessToken)
        {
            return Send(baseUrl, "/users/me", "GET", null, accessToken);
        }

        static async Task<AuthResult> Send(string baseUrl, string path, string method, string json, string accessToken)
        {
            var url = baseUrl.Trim().TrimEnd('/') + path;
            Debug.Log("Drive Sim appelle " + url);

            using var request = new UnityWebRequest(url, method);
            if (!string.IsNullOrEmpty(json))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bytes);
            }
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 12;
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);

            var operation = request.SendWebRequest();
            while (!operation.isDone)
                await Task.Yield();

            var body = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return Fail("Serveur injoignable. Lancez-le sur l'ordinateur, puis vérifiez l'adresse.", false);
            }

            if (path == "/health")
            {
                var health = JsonUtility.FromJson<HealthBody>(body);
                if (request.result == UnityWebRequest.Result.Success && health != null && health.ok)
                    return new AuthResult { Ok = true };
                return Fail("Le serveur ne répond pas correctement.", false);
            }

            if (path == "/auth/logout")
                return new AuthResult { Ok = request.result == UnityWebRequest.Result.Success };

            var unauthorized = request.responseCode == 401;
            if (path == "/users/me")
            {
                var me = string.IsNullOrEmpty(body) ? null : JsonUtility.FromJson<MeBody>(body);
                if (request.result == UnityWebRequest.Result.Success && me != null && me.user != null && !string.IsNullOrEmpty(me.user.id))
                {
                    return new AuthResult
                    {
                        Ok = true,
                        AccessToken = accessToken,
                        Player = ToProfile(me.user)
                    };
                }
                return Fail(ReadError(me != null ? me.error : null, "Profil indisponible."), unauthorized);
            }

            var parsed = string.IsNullOrEmpty(body) ? null : JsonUtility.FromJson<TokenBody>(body);
            if (request.result == UnityWebRequest.Result.Success && parsed != null && !string.IsNullOrEmpty(parsed.accessToken))
            {
                return new AuthResult
                {
                    Ok = true,
                    AccessToken = parsed.accessToken,
                    RefreshToken = parsed.refreshToken,
                    Player = ToProfile(parsed.user)
                };
            }

            var fallback = request.responseCode == 0
                ? "Pas de réponse du serveur."
                : "Le serveur a refusé la demande (" + request.responseCode + ").";
            return Fail(ReadError(parsed != null ? parsed.error : null, fallback), unauthorized);
        }

        static PlayerProfile ToProfile(UserBody user)
        {
            if (user == null) return null;
            return new PlayerProfile(user.id, user.email, user.displayName);
        }

        static string ReadError(string error, string fallback)
        {
            return string.IsNullOrEmpty(error) ? fallback : error;
        }

        static AuthResult Fail(string message, bool unauthorized)
        {
            return new AuthResult { Ok = false, Unauthorized = unauthorized, Error = message };
        }

        [Serializable] class LoginBody { public string email; public string password; }
        [Serializable] class RegisterBody { public string email; public string password; public string displayName; }
        [Serializable] class RefreshBody { public string refreshToken; }
        [Serializable] class UserBody { public string id; public string email; public string displayName; }
        [Serializable] class TokenBody { public string accessToken; public string refreshToken; public UserBody user; public string error; }
        [Serializable] class MeBody { public UserBody user; public string error; }
        [Serializable] class HealthBody { public bool ok; }
    }
}
