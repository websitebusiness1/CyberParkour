using UnityEngine;

/// <summary>
/// Platform that vanishes. Cycle mode: visible -> amber blinking warning -> gone -> back, on a timer.
/// OnStep mode: starts the warning when the player stands on it. The warning is always visible AND audible.
/// </summary>
public class DisappearingPlatform : MonoBehaviour
{
    public enum Mode { Cycle, OnStep }
    public Mode mode = Mode.Cycle;
    public float visibleTime = 3f;
    public float warnTime = 1f;
    public float hiddenTime = 2.5f;
    public float phaseOffset = 0f;
    public Color warnColor = new Color(1f, 0.62f, 0.08f);

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    enum S { Visible, Warning, Hidden }
    S state = S.Visible;
    float stateTimer;
    Renderer[] rends;
    Collider[] cols;
    MaterialPropertyBlock mpb;
    bool wasWarned;

    void Awake()
    {
        rends = GetComponentsInChildren<Renderer>();
        cols = GetComponentsInChildren<Collider>();
        mpb = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (mode == Mode.Cycle)
        {
            float cycle = visibleTime + warnTime + hiddenTime;
            float t = Mathf.Repeat(phaseOffset, cycle);
            if (t < visibleTime) { state = S.Visible; stateTimer = visibleTime - t; }
            else if (t < visibleTime + warnTime) { state = S.Warning; stateTimer = visibleTime + warnTime - t; }
            else { state = S.Hidden; stateTimer = cycle - t; }
            Apply();
        }
        else { state = S.Visible; stateTimer = 0f; }
        var lm = LevelManager.Instance;
        if (lm != null) lm.Respawned += ResetState;
    }

    void OnDestroy()
    {
        var lm = LevelManager.Instance;
        if (lm != null) lm.Respawned -= ResetState;
    }

    void ResetState()
    {
        if (mode == Mode.OnStep) { state = S.Visible; Apply(); }
    }

    /// <summary>Called by PlayerController while the player stands on this platform.</summary>
    public void NotifyStepped()
    {
        if (mode == Mode.OnStep && state == S.Visible)
        {
            state = S.Warning;
            stateTimer = warnTime;
            AudioManager.Play(Sfx.Warning);
        }
    }

    void Update()
    {
        if (state == S.Visible && mode == Mode.OnStep) return;

        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            switch (state)
            {
                case S.Visible: state = S.Warning; stateTimer = warnTime; AudioManager.Play(Sfx.Warning); break;
                case S.Warning: state = S.Hidden; stateTimer = hiddenTime; break;
                case S.Hidden: state = S.Visible; stateTimer = visibleTime; break;
            }
            Apply();
        }

        if (state == S.Warning)
        {
            bool on = ((int)(Time.time * 10f) % 2) == 0;
            foreach (var r in rends)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionId, on ? warnColor * 4f : warnColor * 0.3f);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    void Apply()
    {
        bool solid = state != S.Hidden;
        foreach (var c in cols) if (c != null) c.enabled = solid;
        foreach (var r in rends)
        {
            if (r == null) continue;
            r.enabled = solid;
            if (state != S.Warning)
            {
                r.GetPropertyBlock(mpb);
                mpb.Clear();               // back to the material's own emission
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
