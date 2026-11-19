using UnityEngine;

/// <summary>
/// DDoS pressure meter. Fills over time while the player is in the zone, jumps when packets hit, drains inside
/// protected server racks (CoverZone.coolsOverload). At 100% the player loses a life and is respawned.
/// </summary>
public class OverloadMeter : MonoBehaviour
{
    public static OverloadMeter Instance { get; private set; }

    public float riseRate = 0.012f;      // per second
    public float hitPenalty = 0.12f;
    public float respawnValue = 0.3f;
    public float activeZMin = 0f, activeZMax = 1000f;

    public float Value { get; private set; }

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        var lm = LevelManager.Instance;
        if (lm != null) lm.Respawned += OnRespawn;
    }

    void OnRespawn() { Value = Mathf.Min(Value, respawnValue); }

    public void AddHit() { Value = Mathf.Min(1f, Value + hitPenalty); }
    public void Cool(float amount) { Value = Mathf.Max(0f, Value - amount); }

    void Update()
    {
        var lm = LevelManager.Instance;
        if (lm == null || lm.State != LevelState.Playing) return;
        float pz = lm.player.transform.position.z;
        if (pz < activeZMin || pz > activeZMax) return;

        Value = Mathf.Min(1f, Value + riseRate * Time.deltaTime);
        if (Value >= 1f)
        {
            Value = respawnValue;
            lm.HitAndRespawn("NETWORK OVERLOAD! Find a protected server rack to cool the meter.");
        }
    }
}
