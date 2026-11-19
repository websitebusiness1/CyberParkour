using UnityEngine;

/// <summary>
/// Procedural placeholder animation (lean, bob, squash, slide, hit blink, death, victory spin).
/// To use real animations: assign an Animator. It receives Speed (float), Grounded/Sliding (bool),
/// Jump/Hit/Victory/Die (triggers). Add those parameters to your Animator Controller.
/// </summary>
public class PlayerAnimationController : MonoBehaviour
{
    public Transform visual;
    public Renderer[] renderers;
    public Animator animator;

    PlayerController pc;
    PlayerHealth hp;
    float t, landTimer, lean, roll, scaleY = 1f, yaw;
    bool dead, victory;

    void Awake()
    {
        pc = GetComponent<PlayerController>();
        hp = GetComponent<PlayerHealth>();
    }

    void OnEnable()
    {
        pc = GetComponent<PlayerController>();
        hp = GetComponent<PlayerHealth>();
        pc.Landed += OnLanded;
        pc.Jumped += OnJumped;
        hp.Damaged += OnDamaged;
        hp.Died += OnDied;
    }

    void OnDisable()
    {
        pc.Landed -= OnLanded;
        pc.Jumped -= OnJumped;
        hp.Damaged -= OnDamaged;
        hp.Died -= OnDied;
    }

    void OnLanded() { landTimer = 0.15f; }
    void OnJumped() { Trigger("Jump"); }
    void OnDamaged() { Trigger("Hit"); }
    void OnDied() { dead = true; Trigger("Die"); }

    public void PlayVictory() { victory = true; Trigger("Victory"); }
    public void ResetState() { dead = false; victory = false; yaw = 0f; }

    void Trigger(string name)
    {
        if (animator != null && animator.runtimeAnimatorController != null) animator.SetTrigger(name);
    }

    void Update()
    {
        if (visual == null) return;
        float dt = Time.deltaTime;
        t += dt;
        landTimer = Mathf.Max(0f, landTimer - dt);

        float speed = pc.HorizontalVelocity.magnitude;
        float targetLean = 0f, bob = 0f, targetScaleY = 1f;
        float targetRoll = -Mathf.Clamp(pc.HorizontalVelocity.x / Mathf.Max(0.1f, pc.strafeSpeed), -1f, 1f) * 12f;

        if (dead) { targetLean = -80f; targetRoll = 0f; }
        else if (victory)
        {
            yaw += 360f * dt;
            bob = Mathf.Abs(Mathf.Sin(t * 6f)) * 0.3f;
            targetRoll = 0f;
        }
        else if (pc.IsSliding) { targetLean = 75f; targetScaleY = 0.5f; }
        else if (!pc.IsGrounded)
        {
            targetLean = pc.VerticalVelocity > 0f ? 5f : 15f;
            targetScaleY = 1f + Mathf.Clamp01(Mathf.Abs(pc.VerticalVelocity) / 40f) * 0.15f;
        }
        else if (speed > 0.5f)
        {
            targetLean = pc.IsSprinting ? 22f : 12f;
            bob = Mathf.Abs(Mathf.Sin(t * (pc.IsSprinting ? 16f : 12f))) * 0.08f;
        }
        else targetScaleY = 1f + Mathf.Sin(t * 2f) * 0.01f;

        if (landTimer > 0f) targetScaleY = 0.85f;

        float k = 1f - Mathf.Exp(-12f * dt);
        lean = Mathf.Lerp(lean, targetLean, k);
        roll = Mathf.Lerp(roll, targetRoll, k);
        scaleY = Mathf.Lerp(scaleY, targetScaleY, k);

        visual.localPosition = new Vector3(0f, bob, 0f);
        visual.localRotation = Quaternion.Euler(lean, yaw, roll);
        float xz = 1f + (1f - scaleY) * 0.4f;
        visual.localScale = new Vector3(xz, scaleY, xz);

        // Blink while invulnerable (after a hit or respawn).
        bool show = dead || victory || !hp.IsInvulnerable || ((int)(Time.time * 12f) % 2 == 0);
        if (renderers != null)
            foreach (var r in renderers) if (r != null) r.enabled = show;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat("Speed", speed);
            animator.SetBool("Grounded", pc.IsGrounded);
            animator.SetBool("Sliding", pc.IsSliding);
        }
    }
}
