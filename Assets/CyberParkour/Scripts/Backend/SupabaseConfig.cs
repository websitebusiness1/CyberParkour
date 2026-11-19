using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Connection settings. Lives at Assets/CyberParkour/Resources/SupabaseConfig.asset.
/// Only the PUBLIC anon/publishable key belongs here. The service_role / secret key must NEVER be put
/// in the game client - Validate() refuses it.
/// </summary>
[CreateAssetMenu(menuName = "CyberParkour/Supabase Config")]
public class SupabaseConfig : ScriptableObject
{
    [Tooltip("Example: https://abcdxyz.supabase.co")]
    public string projectUrl = "";
    [Tooltip("ANON / PUBLISHABLE key only. NEVER the service_role / secret key.")]
    public string anonKey = "";

    public string BaseUrl => (projectUrl ?? "").Trim().TrimEnd('/');
    public string Key => (anonKey ?? "").Trim();
    public bool UsesJwtKey => Key.StartsWith("eyJ");

    [Serializable] class JwtPayload { public string role; }

    /// <summary>Returns null when OK, otherwise a human-readable problem.</summary>
    public string Validate()
    {
        if (string.IsNullOrWhiteSpace(projectUrl) || string.IsNullOrWhiteSpace(anonKey))
            return "Supabase URL / key not set. In Unity: Cyber Parkour > Select Supabase Config, then paste them.";
        if (!BaseUrl.StartsWith("https://") && !BaseUrl.StartsWith("http://"))
            return "Project URL must start with https://";
        if (Key.StartsWith("sb_secret_"))
            return "SECURITY: that is a SECRET key. Use the anon/publishable key. Never ship secret keys in the game.";
        if (Key.StartsWith("eyJ"))
        {
            try
            {
                var parts = Key.Split('.');
                if (parts.Length >= 2)
                {
                    var p = JsonUtility.FromJson<JwtPayload>(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
                    if (p != null && p.role == "service_role")
                        return "SECURITY: that is the service_role key. Use the anon key. Never ship service_role in the game.";
                }
            }
            catch { /* not decodable - let the server judge */ }
        }
        return null;
    }

    static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
