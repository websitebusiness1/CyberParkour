using UnityEngine;

/// <summary>Only completes the level if LevelManager's rules are satisfied (checkpoints, tokens).</summary>
public class FinishLineTrigger : MonoBehaviour
{
    public void Reach()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.TryComplete();
    }
}
