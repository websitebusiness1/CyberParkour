using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Calls the trusted database functions (Supabase RPC). The client never sends a balance or an unlock:
/// it only asks, and the server validates the authenticated player, prices, ownership and duplicate requests.
/// </summary>
public class DatabaseService
{
    readonly APIClient api;
    readonly AuthenticationManager auth;

    public DatabaseService(APIClient client, AuthenticationManager authentication) { api = client; auth = authentication; }

    public Task<RpcResponse> EnsureProfile() { return Rpc("ensure_player", "{}"); }
    public Task<RpcResponse> GetProfile() { return Rpc("get_profile", "{}"); }

    public Task<RpcResponse> CompleteLevel(PendingCompletion p)
    {
        return Rpc("complete_level", JsonUtility.ToJson(new CompleteArgs
        {
            p_level = p.level, p_time = p.time, p_coins = p.coins, p_tokens = p.tokens, p_request_id = p.requestId
        }));
    }

    public Task<RpcResponse> PurchaseCharacter(string characterId, string requestId)
    {
        return Rpc("purchase_character", JsonUtility.ToJson(new PurchaseArgs { p_character_id = characterId, p_request_id = requestId }));
    }

    public Task<RpcResponse> SetResearchConsent(bool consent)
    {
        return Rpc("set_research_consent", JsonUtility.ToJson(new ConsentArgs { p_consent = consent }));
    }

    public Task<RpcResponse> SubmitQuiz(string phase, string testSet, int[] answers, string requestId)
    {
        return Rpc("submit_quiz", JsonUtility.ToJson(new QuizArgs { p_phase = phase, p_test_set = testSet, p_answers = answers, p_request_id = requestId }));
    }

    public Task<RpcResponse> SelectCharacter(string characterId)
    {
        return Rpc("select_character", JsonUtility.ToJson(new SelectArgs { p_character_id = characterId }));
    }

    async Task<RpcResponse> Rpc(string function, string json)
    {
        string token = await auth.GetAccessToken();
        if (token == null) return Fail("not_authenticated", null);

        var r = await api.PostJson("/rest/v1/rpc/" + function, json, token);
        if (r.NetworkError) return Fail("network", r.Body);
        if (!r.Success) return Fail("server_error", r.Status + " " + Truncate(r.Body));

        RpcResponse resp = null;
        try { resp = JsonUtility.FromJson<RpcResponse>(r.Body); } catch { }
        return resp ?? Fail("bad_response", Truncate(r.Body));
    }

    static RpcResponse Fail(string code, string detail) { return new RpcResponse { ok = false, error = code, detail = detail }; }
    static string Truncate(string s) { return string.IsNullOrEmpty(s) ? "" : (s.Length > 200 ? s.Substring(0, 200) : s); }

    public static string FriendlyError(string code)
    {
        switch (code)
        {
            case "network": return "Can't reach the database. Check your connection and try again.";
            case "not_authenticated": return "You're not signed in.";
            case "no_profile": return "No player profile found. Try signing in again.";
            case "insufficient_coins": return "Not enough coins.";
            case "already_owned": return "You already own this character.";
            case "unknown_character": return "That character isn't available.";
            case "not_owned": return "You haven't unlocked that character yet.";
            case "level_locked": return "That level is still locked.";
            case "unknown_level": return "Unknown level.";
            case "invalid_time": return "The server rejected the completion time.";
            case "bad_request": return "Bad request.";
            case "consent_required": return "Research consent is needed before results can be saved.";
            case "already_submitted": return "You already submitted this test.";
            case "campaign_incomplete": return "The post-test unlocks after you complete Level 8.";
            case "pre_test_required": return "Take the pre-test first.";
            case "server_error": return "The database returned an error.";
            case "bad_response": return "The database sent an unreadable response.";
            default: return "Something went wrong (" + code + ").";
        }
    }
}
