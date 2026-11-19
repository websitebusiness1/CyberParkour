using UnityEngine;

/// <summary>Sits at a fixed Z and slides sideways toward the player's X once the player is within range ahead of it.
/// Slower than the player's strafe speed, so it can always be dodged.</summary>
public class TrackingHazard : MonoBehaviour
{
    public float trackRange = 18f;
    public float lateralSpeed = 2.6f;
    public float minX = -4f, maxX = 4f;
    public float homeX = 0f;

    void Update()
    {
        var lm = LevelManager.Instance;
        if (lm == null) return;
        Vector3 pp = lm.player.transform.position;
        float dz = transform.position.z - pp.z;
        float target = (dz > -1f && dz < trackRange) ? Mathf.Clamp(pp.x, minX, maxX) : homeX;
        Vector3 p = transform.position;
        p.x = Mathf.MoveTowards(p.x, target, lateralSpeed * Time.deltaTime);
        transform.position = p;
    }
}
