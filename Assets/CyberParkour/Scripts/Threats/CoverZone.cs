using UnityEngine;

/// <summary>
/// Designated safe pocket. While the player is inside:
///  - hazards marked ignoredWhenCovered (surveillance beams / searchlights) cannot detect them
///  - if coolsOverload is set, the network overload meter drains (protected server rack)
/// </summary>
public class CoverZone : MonoBehaviour
{
    public bool coolsOverload = false;
    public float coolRate = 0.18f;   // meter units per second

    public static int Count { get; private set; }
    public static bool PlayerCovered => Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Count = 0; }

    public static void ResetAll() { Count = 0; }

    public void PlayerEntered() { Count++; }
    public void PlayerExited() { Count = Mathf.Max(0, Count - 1); }

    public void PlayerStay(float dt)
    {
        if (coolsOverload && OverloadMeter.Instance != null) OverloadMeter.Instance.Cool(coolRate * dt);
    }
}
