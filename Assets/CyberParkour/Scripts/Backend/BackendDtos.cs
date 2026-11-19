using System;

// Plain data classes matching the JSON the database functions return (JsonUtility-friendly).

[Serializable] public class PlayerDto
{
    public string id;
    public string display_name;
    public int total_coins;
    public int current_unlocked_level;
    public string selected_character_id;
    public bool research_consent;
    public bool consent_answered;
}

[Serializable] public class CharacterDto
{
    public string id, name, description, body_hex, accent_hex, accessory;
    public int unlock_cost;
    public bool is_available, unlocked, selected;
}

[Serializable] public class LevelDto
{
    public int id;
    public string scene_name, threat_name, title;
    public int completion_bonus, max_run_coins, best_coins, best_tokens;
    public bool unlocked, completed, objective_complete;
    public float best_time;
}

[Serializable] public class QuizDto
{
    public bool has_pre, has_post;
    public int pre_score, pre_total, post_score, post_total;
    public int[] pre_threat, post_threat;     // correct answers per threat (index 0 = threat 1)
}

[Serializable] public class ProfileDto
{
    public PlayerDto player;
    public CharacterDto[] characters;
    public LevelDto[] levels;
    public QuizDto quiz;
}

[Serializable] public class RpcResponse
{
    public bool ok;
    public string error;
    public string detail;            // client-side only (network / HTTP details)
    public bool duplicate;
    public int reward;
    public int newly_unlocked_level;
    public ProfileDto profile;
}

[Serializable] public class PendingCompletion
{
    public string userId;
    public int level;
    public float time;
    public int coins;
    public int tokens;
    public string requestId;        // idempotency key, reused on every retry of the same completion
}

// ---- request bodies ----
[Serializable] class CompleteArgs { public int p_level; public float p_time; public int p_coins; public int p_tokens; public string p_request_id; }
[Serializable] class PurchaseArgs { public string p_character_id; public string p_request_id; }
[Serializable] class ConsentArgs { public bool p_consent; }
[Serializable] class QuizArgs { public string p_phase; public string p_test_set; public int[] p_answers; public string p_request_id; }
[Serializable] class SelectArgs { public string p_character_id; }
[Serializable] class CredArgs { public string email; public string password; }
[Serializable] class RefreshArgs { public string refresh_token; }

// ---- auth responses ----
[Serializable] class AuthUser { public string id; public string email; public bool is_anonymous; }
[Serializable] class AuthResponse
{
    public string access_token, refresh_token;
    public int expires_in;
    public AuthUser user;
    public string error, error_code, error_description, msg, message;
}
