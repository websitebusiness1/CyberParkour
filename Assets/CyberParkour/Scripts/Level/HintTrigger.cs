using UnityEngine;

/// <summary>Shows a one-time tutorial/learning message when the player passes through.</summary>
public class HintTrigger : MonoBehaviour
{
    [TextArea] public string message;
    public float duration = 4f;
    bool fired;

    public void Fire()
    {
        if (fired || LevelManager.Instance == null) return;
        fired = true;
        LevelManager.Instance.ShowMessage(message, duration);
    }
}
