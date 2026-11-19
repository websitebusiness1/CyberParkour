using UnityEngine;

/// <summary>Falling streaks of "data". Moves its children downward and wraps them to the top (cheap, no particles).</summary>
public class DataRain : MonoBehaviour
{
    public float minY = -10f;
    public float maxY = 45f;
    public Vector2 speedRange = new Vector2(4f, 12f);
    float[] speeds;

    void Awake()
    {
        speeds = new float[transform.childCount];
        for (int i = 0; i < speeds.Length; i++) speeds[i] = Random.Range(speedRange.x, speedRange.y);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < speeds.Length && i < transform.childCount; i++)
        {
            var t = transform.GetChild(i);
            Vector3 p = t.localPosition;
            p.y -= speeds[i] * dt;
            if (p.y < minY) p.y = maxY;
            t.localPosition = p;
        }
    }
}
