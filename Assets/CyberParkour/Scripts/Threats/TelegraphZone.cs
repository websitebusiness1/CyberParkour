using UnityEngine;

/// <summary>
/// A hazard zone with a fair, repeating cycle: COOLDOWN (safe) -> WARNING (amber marker + sound, still harmless)
/// -> ACTIVE (red, damaging) -> COOLDOWN. The damage collider is only enabled while ACTIVE.
/// Used for scanner beams, corruption bursts, intercept streams.
/// </summary>
public class TelegraphZone : MonoBehaviour
{
    public GameObject warningVisual;
    public GameObject activeVisual;
    public Collider damageCollider;
    public float cooldown = 2f;
    public float warnTime = 1.2f;
    public float activeTime = 1f;
    public float phaseOffset = 0f;
    public float audioRange = 30f;

    enum S { Cooldown, Warning, Active }
    S state;

    void Start()
    {
        Set(S.Cooldown);
    }

    float Cycle => cooldown + warnTime + activeTime;

    void Update()
    {
        float t = Mathf.Repeat(Time.time + phaseOffset, Cycle);
        S next = t < cooldown ? S.Cooldown : t < cooldown + warnTime ? S.Warning : S.Active;
        if (next != state) Set(next);
        if (state == S.Warning && warningVisual != null)
            warningVisual.SetActive(((int)(Time.time * 8f) % 2) == 0);
    }

    void Set(S s)
    {
        state = s;
        if (warningVisual != null) warningVisual.SetActive(s == S.Warning);
        if (activeVisual != null) activeVisual.SetActive(s == S.Active);
        if (damageCollider != null) damageCollider.enabled = s == S.Active;
        if (s == S.Warning)
        {
            var lm = LevelManager.Instance;
            if (lm != null && Mathf.Abs(lm.player.transform.position.z - transform.position.z) < audioRange)
                AudioManager.Play(Sfx.Warning);
        }
    }
}
