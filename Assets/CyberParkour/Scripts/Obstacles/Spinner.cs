using UnityEngine;

/// <summary>Cheap decorative motion: rotation and optional vertical bobbing.</summary>
public class Spinner : MonoBehaviour
{
    public Vector3 degreesPerSecond = new Vector3(0f, 90f, 0f);
    public float bobAmplitude = 0f;
    public float bobSpeed = 1.5f;
    Vector3 start;
    float offset;

    void Awake() { start = transform.position; offset = Random.value * 6.28f; }

    void Update()
    {
        transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
        if (bobAmplitude > 0f)
            transform.position = start + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + offset) * bobAmplitude);
    }
}
