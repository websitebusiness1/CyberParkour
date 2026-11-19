using System;
using UnityEngine;

[Serializable]
public class ChallengeData
{
    public string title = "SECURITY CHECK";
    [TextArea] public string question;
    public string[] options;
    public int correctIndex;
    [TextArea] public string explanation;
}

/// <summary>Pauses the run and shows a multiple-choice security question. Wrong answers explain the concept and
/// allow another try; the correct answer opens the gate(s).</summary>
public class ChallengeTrigger : MonoBehaviour
{
    public ChallengeData data;
    public GateDoor[] gatesToOpen;
    public bool Passed { get; private set; }

    public void Fire()
    {
        if (Passed || LevelManager.Instance == null) return;
        LevelManager.Instance.BeginChallenge(this);
    }

    public void MarkPassed()
    {
        Passed = true;
        if (gatesToOpen != null) foreach (var g in gatesToOpen) if (g != null) g.Open();
    }
}
