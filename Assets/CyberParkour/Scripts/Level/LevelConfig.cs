using UnityEngine;

/// <summary>Per-level data (ScriptableObject). One asset per level; later levels reuse the same systems.</summary>
[CreateAssetMenu(menuName = "CyberParkour/Level Config")]
public class LevelConfig : ScriptableObject
{
    public int levelNumber = 1;
    public string threatName = "Malware";
    public string levelTitle = "The Infected Processor";
    [TextArea] public string objective;
    [TextArea] public string learningSummary;
    [TextArea] public string learningTip;

    [Header("Rewards")]
    public int coinValue = 1;
    public int tokenValue = 5;
    public int completionBonus = 50;
    public float parTimeSeconds = 120f;

    [Header("Rules")]
    public int maxLives = 3;
    public int requiredTokens = 0;
    public int requiredCheckpoints = 1;
    public int requiredChallenges = 0;
    public bool isFinalLevel = false;
    public float killY = -12f;

    [Header("Flow")]
    public string nextSceneName = "Level02_Phishing";
}
