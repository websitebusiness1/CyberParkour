using UnityEngine;

/// <summary>Follow camera behind the runner (looking down +Z) with smoothing and wall collision.</summary>
public class ThirdPersonCameraController : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 3.8f, -8f);
    public float lookHeight = 1.4f;
    public float lookAhead = 7f;
    public float lateralSmooth = 6f;
    public float verticalSmooth = 7f;
    public float forwardSmooth = 14f;
    public float collisionRadius = 0.3f;
    public float minDistance = 1.5f;

    [Header("Speed feel")]
    public float baseFov = 65f;
    public float sprintFov = 74f;

    Vector3 current;
    bool initialised;
    PlayerController pc;
    Camera cam;

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 focus = target.position + Vector3.up * lookHeight;
        Vector3 desired = target.position + offset;

        // Snap after respawn / teleport.
        if (!initialised || (desired - current).sqrMagnitude > 15f * 15f)
        {
            current = desired;
            initialised = true;
        }

        float dt = Time.deltaTime;
        if (pc == null) pc = target.GetComponent<PlayerController>();
        if (cam == null) cam = GetComponent<Camera>();
        if (cam != null)
        {
            float tf = pc != null && (pc.IsSprinting || pc.IsSliding) ? sprintFov : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, tf, 1f - Mathf.Exp(-6f * dt));
        }
        current.x = Mathf.Lerp(current.x, desired.x, 1f - Mathf.Exp(-lateralSmooth * dt));
        current.y = Mathf.Lerp(current.y, desired.y, 1f - Mathf.Exp(-verticalSmooth * dt));
        current.z = Mathf.Lerp(current.z, desired.z, 1f - Mathf.Exp(-forwardSmooth * dt));

        Vector3 pos = current;
        Vector3 dir = pos - focus;
        float dist = dir.magnitude;
        if (dist > 0.01f)
        {
            Vector3 n = dir / dist;
            if (Physics.SphereCast(focus, collisionRadius, n, out RaycastHit hit, dist, ~0, QueryTriggerInteraction.Ignore))
                pos = focus + n * Mathf.Max(minDistance, hit.distance - 0.1f);
        }

        transform.position = pos;
        Vector3 lookPoint = focus + Vector3.forward * lookAhead;
        transform.rotation = Quaternion.LookRotation(lookPoint - pos);
    }
}
