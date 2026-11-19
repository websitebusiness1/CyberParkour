using UnityEngine;

/// <summary>
/// Hazard that hides as harmless scenery (rootkit style). When the player comes within revealDistance it
/// glitches into view with a warning blink, THEN becomes dangerous - so it is hidden but never unfair.
/// </summary>
public class DisguisedHazard : MonoBehaviour
{
    public GameObject disguise;
    public GameObject revealed;
    public Collider damageCollider;
    public float revealDistance = 15f;
    public float warnTime = 0.9f;

    enum S { Hidden, Warning, Armed }
    S state;
    float timer;

    void Start()
    {
        ResetHazard();
        var lm = LevelManager.Instance;
        if (lm != null) lm.Respawned += ResetHazard;
    }

    void OnDestroy()
    {
        var lm = LevelManager.Instance;
        if (lm != null) lm.Respawned -= ResetHazard;
    }

    void ResetHazard()
    {
        state = S.Hidden;
        if (disguise != null) disguise.SetActive(true);
        if (revealed != null) revealed.SetActive(false);
        if (damageCollider != null) damageCollider.enabled = false;
    }

    void Update()
    {
        var lm = LevelManager.Instance;
        if (lm == null) return;
        float dz = transform.position.z - lm.player.transform.position.z;

        if (state == S.Hidden && dz < revealDistance && dz > -3f)
        {
            state = S.Warning;
            timer = warnTime;
            if (disguise != null) disguise.SetActive(false);
            if (revealed != null) revealed.SetActive(true);
            AudioManager.Play(Sfx.Warning);
        }
        else if (state == S.Warning)
        {
            timer -= Time.deltaTime;
            if (revealed != null) revealed.SetActive(((int)(Time.time * 12f) % 2) == 0);
            if (timer <= 0f)
            {
                state = S.Armed;
                if (revealed != null) revealed.SetActive(true);
                if (damageCollider != null) damageCollider.enabled = true;
            }
        }
    }
}
