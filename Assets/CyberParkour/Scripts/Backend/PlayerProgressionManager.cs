using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class OpResult
{
    public bool Ok;
    public bool Retryable;      // true when the failure was a network problem (safe to try again)
    public string Code = "";
    public string Message = "";
    public RpcResponse Response;

    public static OpResult Success(RpcResponse r) { return new OpResult { Ok = true, Response = r }; }
    public static OpResult Fail(string code, string message, bool retryable = false)
    {
        return new OpResult { Ok = false, Code = code, Message = message, Retryable = retryable };
    }
}

/// <summary>
/// Single owner of the session + the last SERVER-CONFIRMED profile. Survives scene loads.
/// Rules: the cached profile is only ever replaced by a successful server response (never by an empty one),
/// and nothing here is credited locally - coins/unlocks appear only after the server confirms them.
/// </summary>
public class PlayerProgressionManager : MonoBehaviour
{
    const string PendingKey = "cs_pending_completion";

    public static PlayerProgressionManager Instance { get; private set; }

    public static PlayerProgressionManager Ensure()
    {
        if (Instance == null)
        {
            var go = new GameObject("PlayerProgressionManager");
            go.AddComponent<PlayerProgressionManager>();   // Awake registers Instance
        }
        return Instance;
    }

    public SupabaseConfig Config { get; private set; }
    public string ConfigProblem { get; private set; }
    public bool IsConfigured => ConfigProblem == null;
    public AuthenticationManager Auth { get; private set; }
    public DatabaseService Db { get; private set; }

    public ProfileDto Profile { get; private set; }
    public bool HasProfile => Profile != null && Profile.player != null && !string.IsNullOrEmpty(Profile.player.id);
    public PendingCompletion Pending { get; private set; }
    public event Action ProfileChanged;

    readonly Dictionary<string, string> purchaseRequestIds = new Dictionary<string, string>();
    readonly Dictionary<string, string> quizRequestIds = new Dictionary<string, string>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Config = Resources.Load<SupabaseConfig>("SupabaseConfig");
        ConfigProblem = Config == null ? "SupabaseConfig asset is missing. Run Cyber Parkour > Build Everything." : Config.Validate();
        if (IsConfigured)
        {
            var api = new APIClient(Config);
            Auth = new AuthenticationManager(api);
            Db = new DatabaseService(api, Auth);
        }
    }

    public CharacterDto SelectedCharacter
    {
        get
        {
            if (!HasProfile || Profile.characters == null) return null;
            foreach (var c in Profile.characters) if (c.selected) return c;
            return null;
        }
    }

    void ApplyProfile(ProfileDto p)
    {
        if (p == null || p.player == null || string.IsNullOrEmpty(p.player.id)) return; // never overwrite with empty
        Profile = p;
        ProfileChanged?.Invoke();
    }

    OpResult FromRpc(RpcResponse r)
    {
        if (r != null && r.ok) { ApplyProfile(r.profile); return OpResult.Success(r); }
        string code = r != null && !string.IsNullOrEmpty(r.error) ? r.error : "unknown";
        string msg = DatabaseService.FriendlyError(code);
        if (r != null && !string.IsNullOrEmpty(r.detail) && code == "server_error") msg += " (" + r.detail + ")";
        return OpResult.Fail(code, msg, code == "network");
    }

    // ---------------- session ----------------
    /// <summary>Restore a saved session (if any) and load the profile.</summary>
    public async Task<OpResult> StartSession()
    {
        if (!IsConfigured) return OpResult.Fail("not_configured", ConfigProblem);
        var s = await Auth.TryRestoreSession();
        if (!s.Success)
            return OpResult.Fail(s.NetworkError ? "network" : "no_session", s.Message, s.NetworkError);
        return await LoadProfile();
    }

    public async Task<OpResult> LoadProfile()
    {
        if (!IsConfigured) return OpResult.Fail("not_configured", ConfigProblem);
        return FromRpc(await Db.EnsureProfile());   // creates the profile on first login
    }

    public async Task SignOut()
    {
        if (Auth != null) await Auth.SignOut();
        Profile = null;
        Pending = null;
        ProfileChanged?.Invoke();
    }

    // ---------------- level completion ----------------
    public PendingCompletion NewCompletion(int level, float time, int coins, int tokens)
    {
        return new PendingCompletion
        {
            userId = Auth != null ? Auth.UserId : "",
            level = level, time = time, coins = coins, tokens = tokens,
            requestId = Guid.NewGuid().ToString()
        };
    }

    /// <summary>Submits (or re-submits with the SAME request id) a completion. Duplicate-safe on the server.</summary>
    public async Task<OpResult> SubmitCompletion(PendingCompletion p)
    {
        if (!IsConfigured || Auth == null || !Auth.IsSignedIn)
            return OpResult.Fail("offline", "Offline run - reward NOT saved. Sign in from the main menu to bank coins.");

        Pending = p;
        PlayerPrefs.SetString(PendingKey, JsonUtility.ToJson(p));
        PlayerPrefs.Save();

        var res = FromRpc(await Db.CompleteLevel(p));
        if (res.Ok || !res.Retryable)
        {
            // saved, or definitively rejected: nothing left to retry
            Pending = null;
            PlayerPrefs.DeleteKey(PendingKey);
            PlayerPrefs.Save();
        }
        return res;
    }

    /// <summary>Re-sends a completion that was interrupted by a network failure (e.g. before the game closed).</summary>
    public async Task<OpResult> FlushPendingCompletion()
    {
        if (!IsConfigured || Auth == null || !Auth.IsSignedIn) return null;
        string json = PlayerPrefs.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(json)) return null;
        PendingCompletion p = null;
        try { p = JsonUtility.FromJson<PendingCompletion>(json); } catch { }
        if (p == null || p.userId != Auth.UserId) return null;
        return await SubmitCompletion(p);
    }

    // ---------------- characters ----------------
    public async Task<OpResult> PurchaseCharacter(string characterId)
    {
        if (!IsConfigured || Auth == null || !Auth.IsSignedIn) return OpResult.Fail("not_authenticated", DatabaseService.FriendlyError("not_authenticated"));

        // Same request id for every retry of the same purchase attempt -> never charged twice.
        if (!purchaseRequestIds.TryGetValue(characterId, out var requestId))
        {
            requestId = Guid.NewGuid().ToString();
            purchaseRequestIds[characterId] = requestId;
        }

        var res = FromRpc(await Db.PurchaseCharacter(characterId, requestId));
        if (res.Ok || !res.Retryable) purchaseRequestIds.Remove(characterId);

        if (!res.Ok && res.Code == "already_owned") await LoadProfile();   // resync (e.g. lost response)
        if (!res.Ok && res.Retryable)
            res.Message = "Connection problem - your purchase was NOT confirmed. Press again to retry (you won't be charged twice).";
        return res;
    }

    // ---------------- research consent + knowledge tests ----------------
    public bool IsOnline => IsConfigured && Auth != null && Auth.IsSignedIn && HasProfile;

    public async Task<OpResult> SetResearchConsent(bool consent)
    {
        if (!IsConfigured || Auth == null || !Auth.IsSignedIn) return OpResult.Fail("not_authenticated", DatabaseService.FriendlyError("not_authenticated"));
        return FromRpc(await Db.SetResearchConsent(consent));
    }

    /// <summary>Submits a finished test. The SERVER scores it. Retries reuse the same request id (no duplicates).</summary>
    public async Task<OpResult> SubmitQuiz(string phase, string testSet, int[] answers)
    {
        if (!IsConfigured || Auth == null || !Auth.IsSignedIn) return OpResult.Fail("not_authenticated", DatabaseService.FriendlyError("not_authenticated"));
        if (!quizRequestIds.TryGetValue(phase, out var requestId))
        {
            requestId = Guid.NewGuid().ToString();
            quizRequestIds[phase] = requestId;
        }
        var res = FromRpc(await Db.SubmitQuiz(phase, testSet, answers, requestId));
        if (res.Ok || !res.Retryable) quizRequestIds.Remove(phase);
        if (!res.Ok && res.Code == "already_submitted") await LoadProfile();
        return res;
    }

    public async Task<OpResult> SelectCharacter(string characterId)
    {
        if (!IsConfigured || Auth == null || !Auth.IsSignedIn) return OpResult.Fail("not_authenticated", DatabaseService.FriendlyError("not_authenticated"));
        return FromRpc(await Db.SelectCharacter(characterId));
    }
}
