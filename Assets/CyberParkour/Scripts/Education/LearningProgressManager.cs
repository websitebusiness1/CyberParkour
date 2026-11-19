using System;
using UnityEngine;

/// <summary>
/// Which Learning Center topics the player has completed (answered the knowledge check correctly).
/// Non-critical convenience data, so it lives in PlayerPrefs (per account on this device).
/// </summary>
public static class LearningProgressManager
{
    static string Owner()
    {
        var pm = PlayerProgressionManager.Instance;
        return pm != null && pm.Auth != null && pm.Auth.IsSignedIn ? pm.Auth.UserId : "offline";
    }

    static string Key(int threat) { return "cs_lc_" + Owner() + "_" + threat; }

    public static bool IsCompleted(int threat) { return PlayerPrefs.GetInt(Key(threat), 0) == 1; }

    public static void MarkCompleted(int threat)
    {
        PlayerPrefs.SetInt(Key(threat), 1);
        PlayerPrefs.Save();
    }

    public static int CompletedCount()
    {
        int n = 0;
        for (int t = 1; t <= 8; t++) if (IsCompleted(t)) n++;
        return n;
    }
}

[Serializable]
public class LocalQuiz
{
    public int score;
    public int total;
    public int[] perThreat;
}

/// <summary>
/// Test results kept on THIS DEVICE ONLY - used in offline/practice mode and when the player declines research consent.
/// Never sent anywhere. Separate from the database results used for the capstone evaluation.
/// </summary>
public static class LocalQuizStore
{
    static string Key(string phase)
    {
        var pm = PlayerProgressionManager.Instance;
        string owner = pm != null && pm.Auth != null && pm.Auth.IsSignedIn ? pm.Auth.UserId : "offline";
        return "cs_quiz_local_" + owner + "_" + phase;
    }

    public static bool Has(string phase) { return PlayerPrefs.HasKey(Key(phase)); }

    public static LocalQuiz Load(string phase)
    {
        string json = PlayerPrefs.GetString(Key(phase), "");
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonUtility.FromJson<LocalQuiz>(json); } catch { return null; }
    }

    public static void Save(string phase, LocalQuiz q)
    {
        PlayerPrefs.SetString(Key(phase), JsonUtility.ToJson(q));
        PlayerPrefs.Save();
    }
}
