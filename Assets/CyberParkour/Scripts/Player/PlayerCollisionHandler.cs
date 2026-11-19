using UnityEngine;

/// <summary>Routes trigger contacts (hazards, cover, coins, checkpoints, finish, hints, challenges) to the right system.</summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerHealth))]
public class PlayerCollisionHandler : MonoBehaviour
{
    public float knockbackSpeed = 6f;
    PlayerController pc;
    PlayerHealth hp;

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        hp = GetComponent<PlayerHealth>();
    }

    void OnTriggerEnter(Collider other) { Handle(other, true); }
    void OnTriggerStay(Collider other) { Handle(other, false); }

    void OnTriggerExit(Collider other)
    {
        var cover = other.GetComponentInParent<CoverZone>();
        if (cover != null) cover.PlayerExited();
    }

    void Handle(Collider other, bool entering)
    {
        var lm = LevelManager.Instance;
        if (lm == null || lm.State != LevelState.Playing) return;

        var hazard = other.GetComponentInParent<Hazard>();
        if (hazard != null)
        {
            if (hazard.ignoredWhenCovered && CoverZone.PlayerCovered) return;   // hidden in cover
            if (hazard.respawnOnHit)
            {
                lm.HitAndRespawn(hazard.hitMessage);
                return;
            }
            if (hp.TakeDamage(hazard.damage))
            {
                pc.Knockback(new Vector3(0f, 0f, -knockbackSpeed));
                if (hazard.addsOverload && OverloadMeter.Instance != null) OverloadMeter.Instance.AddHit();
                if (!string.IsNullOrEmpty(hazard.hitMessage)) lm.ShowMessage(hazard.hitMessage, 3.5f);
            }
            return;
        }

        var cover = other.GetComponentInParent<CoverZone>();
        if (cover != null)
        {
            if (entering) cover.PlayerEntered();
            else cover.PlayerStay(Time.deltaTime);
            return;
        }
        if (!entering) return;

        var coin = other.GetComponentInParent<CoinCollectible>();
        if (coin != null) { coin.Collect(); return; }
        var cp = other.GetComponentInParent<Checkpoint>();
        if (cp != null) { cp.Activate(); return; }
        var fin = other.GetComponentInParent<FinishLineTrigger>();
        if (fin != null) { fin.Reach(); return; }
        var ch = other.GetComponentInParent<ChallengeTrigger>();
        if (ch != null) { ch.Fire(); return; }
        var hint = other.GetComponentInParent<HintTrigger>();
        if (hint != null) hint.Fire();
    }
}
