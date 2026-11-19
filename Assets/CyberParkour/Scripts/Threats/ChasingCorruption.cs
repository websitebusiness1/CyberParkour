using UnityEngine;

/// <summary>
/// A wall of corruption that advances along +Z behind the player (ransomware encrypting the vault).
/// It slower than the sprint speed, so it only catches a player who stalls. Touching it costs a life and respawns
/// at the last checkpoint. Reaching a backup checkpoint pushes it back (safe pathway restored).
/// </summary>
public class ChasingCorruption : MonoBehaviour
{
    public float speed = 5.5f;
    public float acceleration = 0.04f;
    public float triggerZ = 20f;          // starts moving when the player passes this Z
    public float maxZ = 1000f;
    public float resetBehindPlayer = 42f; // after respawn / backup, placed this far behind the player

    public static ChasingCorruption Instance { get; private set; }
    public bool Started => started;
    float beepTimer;
    float startZ;
    float currentSpeed;
    bool started;

    void Awake() { Instance = this; }

    /// <summary>Metres between the player and the front of the corruption wall.</summary>
    public float DistanceBehind
    {
        get
        {
            var lm = LevelManager.Instance;
            return lm == null ? 999f : lm.player.transform.position.z - (transform.position.z + 3f);
        }
    }

    void Start()
    {
        startZ = transform.position.z;
        currentSpeed = speed;
        var lm = LevelManager.Instance;
        if (lm != null)
        {
            lm.Respawned += PushBack;
            lm.CheckpointActivated += PushBack;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        var lm = LevelManager.Instance;
        if (lm != null)
        {
            lm.Respawned -= PushBack;
            lm.CheckpointActivated -= PushBack;
        }
    }

    void PushBack()
    {
        var lm = LevelManager.Instance;
        if (lm == null) return;
        Vector3 p = transform.position;
        p.z = Mathf.Min(p.z, lm.player.transform.position.z - resetBehindPlayer);
        p.z = Mathf.Max(p.z, startZ - 40f);
        transform.position = p;
        currentSpeed = speed;
    }

    void Update()
    {
        var lm = LevelManager.Instance;
        if (lm == null || lm.State != LevelState.Playing) return;
        if (!started && lm.player.transform.position.z >= triggerZ) started = true;
        if (!started) return;
        currentSpeed += acceleration * Time.deltaTime;
        Vector3 p = transform.position;
        p.z = Mathf.Min(maxZ, p.z + currentSpeed * Time.deltaTime);
        transform.position = p;

        // The wall is behind the camera, so warn the player with sound as it closes in.
        float d = DistanceBehind;
        if (d < 18f)
        {
            beepTimer -= Time.deltaTime;
            if (beepTimer <= 0f) { AudioManager.Play(Sfx.Warning); beepTimer = Mathf.Lerp(0.25f, 1.1f, Mathf.Clamp01(d / 18f)); }
        }
    }
}
