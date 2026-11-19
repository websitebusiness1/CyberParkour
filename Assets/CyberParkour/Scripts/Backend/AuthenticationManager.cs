using System;
using System.Threading.Tasks;
using UnityEngine;

public class AuthResult
{
    public bool Success, NetworkError, NeedsEmailConfirmation;
    public string Message = "";
}

/// <summary>
/// Supabase Auth over REST: email+password and anonymous guest sessions.
/// Passwords are never stored. Only the refresh token is kept (PlayerPrefs) so the session survives restarts;
/// it can be revoked server-side. Access tokens live in memory only.
/// </summary>
public class AuthenticationManager
{
    const string RefreshKey = "cs_refresh_token";
    readonly APIClient api;
    string accessToken, refreshToken;
    DateTime expiresAtUtc;
    Task<AuthResult> refreshing;

    public string UserId { get; private set; }
    public string Email { get; private set; }
    public bool IsGuest { get; private set; }
    public bool IsSignedIn => !string.IsNullOrEmpty(refreshToken) && !string.IsNullOrEmpty(UserId);

    public AuthenticationManager(APIClient client) { api = client; }

    public async Task<AuthResult> SignUp(string email, string password)
    {
        var bad = ValidateCredentials(email, password);
        if (bad != null) return Fail(bad);
        var r = await api.PostJson("/auth/v1/signup", JsonUtility.ToJson(new CredArgs { email = email.Trim(), password = password }), null);
        return Handle(r);
    }

    public async Task<AuthResult> SignIn(string email, string password)
    {
        var bad = ValidateCredentials(email, password);
        if (bad != null) return Fail(bad);
        var r = await api.PostJson("/auth/v1/token?grant_type=password", JsonUtility.ToJson(new CredArgs { email = email.Trim(), password = password }), null);
        return Handle(r);
    }

    /// <summary>Anonymous account (needs "Allow anonymous sign-ins" enabled in Supabase).</summary>
    public async Task<AuthResult> SignInAsGuest()
    {
        var r = await api.PostJson("/auth/v1/signup", "{}", null);
        return Handle(r);
    }

    public async Task<AuthResult> TryRestoreSession()
    {
        string rt = PlayerPrefs.GetString(RefreshKey, "");
        if (string.IsNullOrEmpty(rt)) return new AuthResult { Success = false, Message = "" };
        return await RefreshWith(rt);
    }

    public async Task SignOut()
    {
        if (!string.IsNullOrEmpty(accessToken))
            await api.PostJson("/auth/v1/logout", "{}", accessToken, 1); // best effort
        ClearLocal();
    }

    /// <summary>Returns a valid access token (refreshing if needed) or null.</summary>
    public async Task<string> GetAccessToken()
    {
        if (!IsSignedIn) return null;
        if (DateTime.UtcNow >= expiresAtUtc.AddSeconds(-60))
        {
            if (refreshing == null) refreshing = RefreshWith(refreshToken);
            var r = await refreshing;
            refreshing = null;
            if (!r.Success) return null;
        }
        return accessToken;
    }

    // ---------------- internals ----------------
    async Task<AuthResult> RefreshWith(string rt)
    {
        var r = await api.PostJson("/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(new RefreshArgs { refresh_token = rt }), null);
        var res = Handle(r);
        // An explicit server rejection means the stored token is dead: forget it. Network errors keep it.
        if (!res.Success && !res.NetworkError) ClearLocal();
        return res;
    }

    AuthResult Handle(ApiResult r)
    {
        if (r.NetworkError)
            return new AuthResult { NetworkError = true, Message = "Can't reach the server. Check your internet connection and try again." };

        AuthResponse a = null;
        try { a = JsonUtility.FromJson<AuthResponse>(r.Body); } catch { }

        if (!r.Success) return Fail(FriendlyError(a, r));
        if (a == null) return Fail("Unexpected response from the server.");

        if (string.IsNullOrEmpty(a.access_token))
            return new AuthResult { NeedsEmailConfirmation = true, Message = "Account created. Check your email to confirm it, then sign in." };

        accessToken = a.access_token;
        refreshToken = a.refresh_token;
        expiresAtUtc = DateTime.UtcNow.AddSeconds(a.expires_in > 0 ? a.expires_in : 3600);
        UserId = a.user != null ? a.user.id : null;
        Email = a.user != null ? a.user.email : null;
        IsGuest = a.user != null && a.user.is_anonymous;
        PlayerPrefs.SetString(RefreshKey, refreshToken);
        PlayerPrefs.Save();
        return new AuthResult { Success = true };
    }

    void ClearLocal()
    {
        accessToken = refreshToken = UserId = Email = null;
        IsGuest = false;
        PlayerPrefs.DeleteKey(RefreshKey);
        PlayerPrefs.Save();
    }

    static AuthResult Fail(string msg) { return new AuthResult { Success = false, Message = msg }; }

    static string ValidateCredentials(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@") || !email.Contains(".")) return "Enter a valid email address.";
        if (string.IsNullOrEmpty(password) || password.Length < 8) return "Password must be at least 8 characters.";
        return null;
    }

    static string FriendlyError(AuthResponse a, ApiResult r)
    {
        string code = a != null ? (!string.IsNullOrEmpty(a.error_code) ? a.error_code : a.error) : null;
        switch (code)
        {
            case "invalid_credentials": return "Wrong email or password.";
            case "user_already_exists":
            case "email_exists": return "That email is already registered. Try signing in.";
            case "email_not_confirmed": return "Please confirm your email first (check your inbox).";
            case "weak_password": return "That password is too weak. Use a longer, less common password.";
            case "anonymous_provider_disabled": return "Guest login is disabled. Enable 'Allow anonymous sign-ins' in Supabase (Authentication > Sign In / Providers).";
            case "over_request_rate_limit":
            case "over_email_send_rate_limit": return "Too many attempts. Wait a minute and try again.";
            case "signup_disabled": return "Sign-ups are disabled for this project.";
        }
        string text = a != null ? (a.error_description ?? a.msg ?? a.message) : null;
        if (!string.IsNullOrEmpty(text)) return text;
        return "Server error (" + r.Status + ").";
    }
}
