using UnityEngine;

/// <summary>Platform that eases between two offsets. PlayerController reads Delta to carry the player.
/// Runs before the player (negative execution order) so the carry is applied in the same frame.</summary>
[DefaultExecutionOrder(-100)]
public class MovingPlatform : MonoBehaviour
{
    public Vector3 offsetA = Vector3.zero;
    public Vector3 offsetB = new Vector3(0f, 0f, 6f);
    public float speed = 3f;
    [Range(0f, 1f)] public float phase = 0f;

    public Vector3 Delta { get; private set; }

    Vector3 a, b, last;
    float length;

    void Start()
    {
        a = transform.position + offsetA;
        b = transform.position + offsetB;
        length = Mathf.Max(0.1f, Vector3.Distance(a, b));
        last = Evaluate();
        transform.position = last;
    }

    Vector3 Evaluate()
    {
        float p = Mathf.PingPong(Time.time * speed / length + phase, 1f);
        return Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, p));
    }

    void Update()
    {
        Vector3 np = Evaluate();
        Delta = np - last;
        transform.position = np;
        last = np;
    }
}
