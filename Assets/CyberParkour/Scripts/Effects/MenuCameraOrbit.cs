using UnityEngine;

/// <summary>Slow cinematic camera sweep around the showroom runner for the main menu.</summary>
public class MenuCameraOrbit : MonoBehaviour
{
    public Vector3 target = new Vector3(0f, 1.1f, 0f);
    public float radius = 6.5f;
    public float height = 2.0f;
    public float sweepDegrees = 32f;
    public float speed = 0.15f;
    [Tooltip("Negative shifts the runner to the right of the screen (menu panel sits on the left).")]
    public float lookShift = -2.4f;

    void LateUpdate()
    {
        float a = Mathf.Sin(Time.time * speed) * sweepDegrees * Mathf.Deg2Rad;
        Vector3 pos = target + new Vector3(Mathf.Sin(a) * radius, height + Mathf.Sin(Time.time * 0.3f) * 0.25f, -Mathf.Cos(a) * radius);
        Vector3 forward = (target - pos); forward.y = 0f;
        Vector3 right = Vector3.Cross(Vector3.up, forward.normalized);
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation((target + right * lookShift) - pos);
    }
}
