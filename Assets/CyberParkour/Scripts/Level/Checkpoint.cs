using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public Transform respawnPoint;
    public Renderer[] indicators;
    bool activated;

    public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    public void Activate()
    {
        if (activated) return;
        activated = true;
        LevelManager.Instance.OnCheckpointReached(this);
        var green = new Color(0.1f, 1f, 0.5f);
        foreach (var r in indicators)
        {
            if (r == null) continue;
            var pulse = r.GetComponent<Pulse>();
            if (pulse != null) pulse.color = green;
            var m = r.material; // instance, so other checkpoints are unaffected
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", green);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", green);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", green * 2f);
        }
    }
}
