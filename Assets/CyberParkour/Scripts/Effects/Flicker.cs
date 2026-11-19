using UnityEngine;

/// <summary>Glitch effect for infected objects: random short bursts of jitter and flicker. Visual only.</summary>
public class Flicker : MonoBehaviour
{
    public float minInterval = 0.4f;
    public float maxInterval = 2.2f;
    public float glitchTime = 0.09f;
    public float jitter = 0.12f;

    Vector3 basePos, baseScale;
    Renderer[] rends;
    float next, endGlitch;
    bool glitching;

    void Awake()
    {
        basePos = transform.localPosition;
        baseScale = transform.localScale;
        rends = GetComponentsInChildren<Renderer>();
        next = Time.time + Random.Range(minInterval, maxInterval);
    }

    void Update()
    {
        if (Time.time >= next)
        {
            endGlitch = Time.time + glitchTime;
            next = Time.time + Random.Range(minInterval, maxInterval);
        }

        if (Time.time < endGlitch)
        {
            glitching = true;
            transform.localPosition = basePos + Random.insideUnitSphere * jitter;
            transform.localScale = Vector3.Scale(baseScale, new Vector3(Random.Range(0.9f, 1.15f), Random.Range(0.85f, 1.2f), Random.Range(0.9f, 1.15f)));
            foreach (var r in rends) if (r != null) r.enabled = Random.value > 0.25f;
        }
        else if (glitching)
        {
            glitching = false;
            transform.localPosition = basePos;
            transform.localScale = baseScale;
            foreach (var r in rends) if (r != null) r.enabled = true;
        }
    }
}
