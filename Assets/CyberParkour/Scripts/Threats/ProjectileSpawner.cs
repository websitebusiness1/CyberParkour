using System;
using UnityEngine;

/// <summary>
/// Launches telegraphed waves of pooled projectiles from lane points while the player is inside the zone.
/// Every wave is announced by lane markers (visual) + a warning sound BEFORE anything is fired.
/// Patterns are authored to always leave at least one open lane.
/// </summary>
public class ProjectileSpawner : MonoBehaviour
{
    [Serializable] public class LanePattern { public bool[] lanes; }

    public GameObject template;
    public Transform[] lanes;              // spawn points, one per lane
    public GameObject[] laneMarkers;       // warning strips, one per lane
    public LanePattern[] patterns;
    public Vector3 direction = new Vector3(0f, 0f, -1f);
    public float speed = 14f;
    public float lifetime = 4f;
    public float interval = 2.4f;          // time between waves (includes warnTime)
    public float warnTime = 1.1f;
    public float activeZMin = 0f, activeZMax = 100f;   // world-Z range in which the player triggers waves

    float timer;
    int patternIndex;
    bool warning;

    void Start()
    {
        timer = interval;
        SetMarkers(null);
    }

    void Update()
    {
        var lm = LevelManager.Instance;
        if (lm == null || lm.State != LevelState.Playing || patterns == null || patterns.Length == 0) return;

        float pz = lm.player.transform.position.z;
        if (pz < activeZMin || pz > activeZMax)
        {
            warning = false;
            SetMarkers(null);
            timer = interval;
            return;
        }

        timer -= Time.deltaTime;
        var pattern = patterns[patternIndex % patterns.Length];

        if (!warning && timer <= warnTime)
        {
            warning = true;
            SetMarkers(pattern);
            AudioManager.Play(Sfx.Warning);
        }
        if (timer <= 0f)
        {
            Fire(pattern);
            warning = false;
            SetMarkers(null);
            patternIndex++;
            timer = interval;
        }
    }

    void SetMarkers(LanePattern p)
    {
        if (laneMarkers == null) return;
        for (int i = 0; i < laneMarkers.Length; i++)
            if (laneMarkers[i] != null) laneMarkers[i].SetActive(p != null && i < p.lanes.Length && p.lanes[i]);
    }

    void Fire(LanePattern p)
    {
        for (int i = 0; i < lanes.Length && i < p.lanes.Length; i++)
        {
            if (!p.lanes[i]) continue;
            var go = ObjectPoolManager.Get(template);
            go.transform.position = lanes[i].position;
            go.transform.rotation = lanes[i].rotation;
            var proj = go.GetComponent<Projectile>();
            if (proj != null) proj.Launch(direction.normalized * speed, lifetime);
        }
    }
}
