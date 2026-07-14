using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SupabaseClient : MonoBehaviour
{
    const string PlayerIdKey = "TeleRunner.Supabase.PlayerId";
    const string PlayerNameKey = "TeleRunner.Supabase.DisplayName";
    const string PlayerPhoneKey = "TeleRunner.Supabase.Phone";
    const string PlayerEmailKey = "TeleRunner.Supabase.Email";
    const string SessionTokenKey = "TeleRunner.Supabase.SessionToken";

    public static SupabaseClient instance;

    SupabaseConfig m_Config;
    string m_PlayerId;
    string m_DisplayName;
    string m_Phone;
    string m_Email;
    string m_SessionToken;

    public bool IsConfigured
    {
        get
        {
            return m_Config != null
                && !string.IsNullOrEmpty(m_Config.projectUrl)
                && !string.IsNullOrEmpty(m_Config.publishableKey)
                && !m_Config.projectUrl.Contains("YOUR_PROJECT_REF")
                && !m_Config.publishableKey.Contains("YOUR_SUPABASE_PUBLISHABLE_KEY");
        }
    }

    public bool HasLocalPlayer
    {
        get { return !string.IsNullOrEmpty(m_PlayerId) && !string.IsNullOrEmpty(m_SessionToken); }
    }

    public string PlayerId { get { return m_PlayerId; } }
    public string DisplayName { get { return string.IsNullOrEmpty(m_DisplayName) ? "Player" : m_DisplayName; } }
    public string Phone { get { return m_Phone; } }
    public string Email { get { return m_Email; } }
    public int LeaderboardLimit { get { return m_Config == null || m_Config.leaderboardLimit <= 0 ? 50 : m_Config.leaderboardLimit; } }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadConfig();
        LoadLocalPlayer();
    }

    public void Register(string displayName, string phone, string email, string password, Action<SupabaseResult> callback)
    {
        StartCoroutine(RegisterRoutine(displayName, phone, email, password, callback));
    }

    public void SignIn(string phone, string password, Action<SupabaseResult> callback)
    {
        StartCoroutine(SignInRoutine(phone, password, callback));
    }

    public void SubmitScore(int score, Action<SupabaseResult> callback)
    {
        StartCoroutine(SubmitScoreRoutine(score, callback));
    }

    public void FetchLeaderboard(int limit, Action<SupabaseLeaderboardResult> callback)
    {
        StartCoroutine(FetchLeaderboardRoutine(limit, callback));
    }

    public void FetchLocalLeaderboardRank(Action<SupabaseRankResult> callback)
    {
        StartCoroutine(FetchLocalLeaderboardRankRoutine(callback));
    }

    IEnumerator RegisterRoutine(string displayName, string phone, string email, string password, Action<SupabaseResult> callback)
    {
        if (!IsConfigured)
        {
            callback?.Invoke(SupabaseResult.Failure("SupabaseConfig.json still has placeholder values."));
            yield break;
        }

        string emailJson = string.IsNullOrEmpty(email) ? "null" : "\"" + EscapeJson(email) + "\"";
        string json = "{\"p_display_name\":\"" + EscapeJson(displayName) + "\",\"p_phone\":\"" + EscapeJson(phone) + "\",\"p_email\":" + emailJson + ",\"p_password\":\"" + EscapeJson(password) + "\"}";

        yield return SendJson("/rest/v1/rpc/register_player", "POST", json, (ok, body, code) =>
        {
            if (!ok)
            {
                callback?.Invoke(SupabaseResult.Failure(ExtractError(body, code)));
                return;
            }

            PlayerRow player = ParseSinglePlayer(body);
            if (player == null || string.IsNullOrEmpty(player.id) || string.IsNullOrEmpty(player.session_token))
            {
                callback?.Invoke(SupabaseResult.Failure("Supabase did not return the new player session."));
                return;
            }

            StorePlayer(player);
            callback?.Invoke(SupabaseResult.Success("Registered."));
        });
    }

    IEnumerator SignInRoutine(string phone, string password, Action<SupabaseResult> callback)
    {
        if (!IsConfigured)
        {
            callback?.Invoke(SupabaseResult.Failure("SupabaseConfig.json still has placeholder values."));
            yield break;
        }

        string json = "{\"p_phone\":\"" + EscapeJson(phone) + "\",\"p_password\":\"" + EscapeJson(password) + "\"}";

        yield return SendJson("/rest/v1/rpc/sign_in_player", "POST", json, (ok, body, code) =>
        {
            if (!ok)
            {
                callback?.Invoke(SupabaseResult.Failure(ExtractError(body, code)));
                return;
            }

            PlayerRow player = ParseSinglePlayer(body);
            if (player == null || string.IsNullOrEmpty(player.id) || string.IsNullOrEmpty(player.session_token))
            {
                callback?.Invoke(SupabaseResult.Failure("Invalid phone or password."));
                return;
            }

            StorePlayer(player);
            callback?.Invoke(SupabaseResult.Success("Signed in."));
        });
    }

    IEnumerator SubmitScoreRoutine(int score, Action<SupabaseResult> callback)
    {
        if (!IsConfigured)
        {
            callback?.Invoke(SupabaseResult.Failure("SupabaseConfig.json still has placeholder values."));
            yield break;
        }

        if (!HasLocalPlayer)
        {
            callback?.Invoke(SupabaseResult.Failure("Player is not registered or signed in."));
            yield break;
        }

        string json = "{\"p_app_player_id\":\"" + EscapeJson(m_PlayerId) + "\",\"p_session_token\":\"" + EscapeJson(m_SessionToken) + "\",\"p_score\":" + Mathf.Max(score, 0) + "}";
        yield return SendJson("/rest/v1/rpc/submit_score", "POST", json, (ok, body, code) =>
        {
            callback?.Invoke(ok ? SupabaseResult.Success("Score submitted.") : SupabaseResult.Failure(ExtractError(body, code)));
        });
    }

    IEnumerator FetchLeaderboardRoutine(int limit, Action<SupabaseLeaderboardResult> callback)
    {
        if (!IsConfigured)
        {
            callback?.Invoke(SupabaseLeaderboardResult.Failure("SupabaseConfig.json still has placeholder values."));
            yield break;
        }

        int safeLimit = Mathf.Clamp(limit <= 0 ? LeaderboardLimit : limit, 1, 100);
        string path = "/rest/v1/leaderboard?select=rank,player_name,score&order=rank.asc&limit=" + safeLimit;
        yield return SendJson(path, "GET", null, (ok, body, code) =>
        {
            if (!ok)
            {
                callback?.Invoke(SupabaseLeaderboardResult.Failure(ExtractError(body, code)));
                return;
            }

            LeaderboardRows wrapper = JsonUtility.FromJson<LeaderboardRows>("{\"items\":" + body + "}");
            callback?.Invoke(SupabaseLeaderboardResult.Success(wrapper == null || wrapper.items == null ? new List<LeaderboardRow>() : wrapper.items));
        });
    }
    IEnumerator FetchLocalLeaderboardRankRoutine(Action<SupabaseRankResult> callback)
    {
        if (!IsConfigured)
        {
            callback?.Invoke(SupabaseRankResult.Failure("SupabaseConfig.json still has placeholder values."));
            yield break;
        }

        if (!HasLocalPlayer)
        {
            callback?.Invoke(SupabaseRankResult.Failure("Player is not registered or signed in."));
            yield break;
        }

        string encodedName = UnityWebRequest.EscapeURL(DisplayName);
        string path = "/rest/v1/leaderboard?select=rank,player_name,score&player_name=eq." + encodedName + "&order=score.desc&limit=1";
        yield return SendJson(path, "GET", null, (ok, body, code) =>
        {
            if (!ok)
            {
                callback?.Invoke(SupabaseRankResult.Failure(ExtractError(body, code)));
                return;
            }

            LeaderboardRows wrapper = JsonUtility.FromJson<LeaderboardRows>("{\"items\":" + body + "}");
            if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
            {
                callback?.Invoke(SupabaseRankResult.Failure("No leaderboard score yet."));
                return;
            }

            LeaderboardRow row = wrapper.items[0];
            callback?.Invoke(SupabaseRankResult.Success(row.rank, row.score));
        });
    }

    IEnumerator SendJson(string path, string method, string json, Action<bool, string, long> callback)
    {
        string url = m_Config.projectUrl.TrimEnd('/') + path;
        using (UnityWebRequest request = new UnityWebRequest(url, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            if (!string.IsNullOrEmpty(json))
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));

            request.SetRequestHeader("apikey", m_Config.publishableKey);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 300;
            callback?.Invoke(ok, request.downloadHandler == null ? "" : request.downloadHandler.text, request.responseCode);
        }
    }

    void LoadConfig()
    {
        TextAsset configAsset = Resources.Load<TextAsset>("SupabaseConfig");
        if (configAsset == null)
        {
            Debug.LogWarning("SupabaseConfig.json was not found in Resources.");
            return;
        }

        m_Config = JsonUtility.FromJson<SupabaseConfig>(configAsset.text);
        if (m_Config != null && !string.IsNullOrEmpty(m_Config.projectUrl))
            m_Config.projectUrl = m_Config.projectUrl.TrimEnd('/');
    }

    void LoadLocalPlayer()
    {
        m_PlayerId = PlayerPrefs.GetString(PlayerIdKey, "");
        m_DisplayName = PlayerPrefs.GetString(PlayerNameKey, "");
        m_Phone = PlayerPrefs.GetString(PlayerPhoneKey, "");
        m_Email = PlayerPrefs.GetString(PlayerEmailKey, "");
        m_SessionToken = PlayerPrefs.GetString(SessionTokenKey, "");
    }

    void StorePlayer(PlayerRow player)
    {
        m_PlayerId = player.id;
        m_DisplayName = player.display_name;
        m_Phone = player.phone;
        m_Email = player.email;
        m_SessionToken = player.session_token;

        PlayerPrefs.SetString(PlayerIdKey, m_PlayerId);
        PlayerPrefs.SetString(PlayerNameKey, string.IsNullOrEmpty(m_DisplayName) ? "" : m_DisplayName);
        PlayerPrefs.SetString(PlayerPhoneKey, string.IsNullOrEmpty(m_Phone) ? "" : m_Phone);
        PlayerPrefs.SetString(PlayerEmailKey, string.IsNullOrEmpty(m_Email) ? "" : m_Email);
        PlayerPrefs.SetString(SessionTokenKey, m_SessionToken);
        PlayerPrefs.Save();
    }

    PlayerRow ParseSinglePlayer(string body)
    {
        if (string.IsNullOrEmpty(body))
            return null;

        try
        {
            PlayerRows wrapper;
            if (body.TrimStart().StartsWith("[", StringComparison.Ordinal))
                wrapper = JsonUtility.FromJson<PlayerRows>("{\"items\":" + body + "}");
            else
                wrapper = new PlayerRows { items = new List<PlayerRow> { JsonUtility.FromJson<PlayerRow>(body) } };

            if (wrapper == null || wrapper.items == null || wrapper.items.Count == 0)
                return null;

            return wrapper.items[0];
        }
        catch (Exception e)
        {
            Debug.LogWarning("Unable to parse Supabase player response: " + e.Message);
            return null;
        }
    }

    string ExtractError(string body, long responseCode)
    {
        if (string.IsNullOrEmpty(body))
            return "Supabase request failed with HTTP " + responseCode + ".";

        try
        {
            SupabaseError error = JsonUtility.FromJson<SupabaseError>(body);
            if (error != null)
            {
                if (!string.IsNullOrEmpty(error.msg))
                    return error.msg;
                if (!string.IsNullOrEmpty(error.message))
                    return error.message;
                if (!string.IsNullOrEmpty(error.hint))
                    return error.hint;
                if (!string.IsNullOrEmpty(error.details))
                    return error.details;
                if (!string.IsNullOrEmpty(error.code))
                    return error.code;
            }
        }
        catch
        {
        }

        return body;
    }

    string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        StringBuilder builder = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; ++i)
        {
            char c = value[i];
            switch (c)
            {
                case '\\': builder.Append("\\\\"); break;
                case '\"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ')
                        builder.Append("\\u" + ((int)c).ToString("x4"));
                    else
                        builder.Append(c);
                    break;
            }
        }

        return builder.ToString();
    }

    [Serializable]
    class SupabaseConfig
    {
        public string projectUrl;
        public string publishableKey;
        public int leaderboardLimit = 50;
    }

    [Serializable]
    class SupabaseError
    {
        public string msg;
        public string message;
        public string details;
        public string hint;
        public string code;
    }

    [Serializable]
    class PlayerRows
    {
        public List<PlayerRow> items;
    }

    [Serializable]
    class PlayerRow
    {
        public string id;
        public string display_name;
        public string phone;
        public string email;
        public string session_token;
    }

    [Serializable]
    class LeaderboardRows
    {
        public List<LeaderboardRow> items;
    }

    [Serializable]
    public class LeaderboardRow
    {
        public int rank;
        public string player_name;
        public int score;
    }

    public class SupabaseResult
    {
        public bool success;
        public string message;

        public static SupabaseResult Success(string message)
        {
            return new SupabaseResult { success = true, message = message };
        }

        public static SupabaseResult Failure(string message)
        {
            return new SupabaseResult { success = false, message = message };
        }
    }

    public class SupabaseRankResult : SupabaseResult
    {
        public int rank;
        public int score;

        public static SupabaseRankResult Success(int rank, int score)
        {
            return new SupabaseRankResult { success = true, rank = rank, score = score };
        }

        public new static SupabaseRankResult Failure(string message)
        {
            return new SupabaseRankResult { success = false, message = message, rank = 0, score = 0 };
        }
    }

    public class SupabaseLeaderboardResult : SupabaseResult
    {
        public List<LeaderboardRow> rows;

        public static SupabaseLeaderboardResult Success(List<LeaderboardRow> rows)
        {
            return new SupabaseLeaderboardResult { success = true, rows = rows };
        }

        public new static SupabaseLeaderboardResult Failure(string message)
        {
            return new SupabaseLeaderboardResult { success = false, message = message, rows = new List<LeaderboardRow>() };
        }
    }
}
