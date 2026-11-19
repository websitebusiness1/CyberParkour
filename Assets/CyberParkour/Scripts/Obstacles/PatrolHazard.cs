using UnityEngine;

/// <summary>Moves a hazard back and forth between two offsets (eased, so it slows at the ends = readable).</summary>
public class PatrolHazard : MonoBehaviour
{
    public Vector3 offsetA = new Vector3(-4f, 0f, 0f);
    public Vector3 offsetB = new Vector3(4f, 0f, 0f);
    public float speed = 3.5f;
    [Range(0f, 1f)] public float phase = 0f;

    Vector3 a, b;
    float length;

    void Start()
    {
        a = transform.position + offsetA;
        b = transform.position + offsetB;
        length = Mathf.Max(0.1f, Vector3.Distance(a, b));
    }

    void Update()
    {
        float p = Mathf.PingPong(Time.time * speed / length + phase * 1f, 1f);
        transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, p));
    }
}
