using UnityEngine;

/// <summary>Pulses emission brightness (warning lights, gates, portals). Uses a MaterialPropertyBlock, no material copies.</summary>
public class Pulse : MonoBehaviour
{
    public Color color = Color.white;
    public float minIntensity = 0.6f;
    public float maxIntensity = 3.5f;
    public float speed = 2.5f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    Renderer[] rends;
    MaterialPropertyBlock mpb;
    float phase;

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        phase = Random.value * 6.28f;
    }

    void Update()
    {
        float k = (Mathf.Sin(Time.time * speed + phase) + 1f) * 0.5f;
        Color c = color * Mathf.Lerp(minIntensity, maxIntensity, k);
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor(EmissionId, c);
            r.SetPropertyBlock(mpb);
        }
    }
}
