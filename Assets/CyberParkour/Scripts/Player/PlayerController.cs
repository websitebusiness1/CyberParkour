using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Arcade third-person movement built on CharacterController (no Rigidbody on the player).
/// Course direction is +Z. Handles run, sprint, strafe, jump (coyote + buffer), slide, knockback.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Speed")]
    public float runSpeed = 8f;
    public float sprintSpeed = 12f;
    public float strafeSpeed = 7f;
    public float backSpeed = 4f;
    public float groundAcceleration = 60f;
    public float airAcceleration = 30f;
    [Tooltip("If true the runner moves forward on its own (Subway Surfers style).")]
    public bool autoRun = false;

    [Header("Jump")]
    public float jumpHeight = 1.7f;
    public float gravity = -32f;
    public float maxFallSpeed = -40f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Slide")]
    public float slideDuration = 0.8f;
    public float slideSpeed = 13f;
    public float slideCooldown = 0.25f;

    public const float StandHeight = 2f;
    public const float SlideHeight = 1f;

    public bool InputEnabled { get; set; } = true;
    public bool IsGrounded { get; private set; }
    public bool IsSliding { get; private set; }
    public bool IsSprinting { get; private set; }
    public Vector3 HorizontalVelocity => new Vector3(velocity.x, 0f, velocity.z);
    public float VerticalVelocity => velocity.y;

    public event Action Jumped;
    public event Action Landed;
    public event Action SlideStarted;

    CharacterController cc;
    Vector3 velocity;
    Vector3 external;
    float lastGroundedTime = -10f;
    float lastJumpPressed = -10f;
    float slideEnd, slideReady;
    float airTime;
    readonly Collider[] overlapBuffer = new Collider[8];
    MovingPlatform ridingPlatform;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cc.height = StandHeight;
        cc.center = new Vector3(0f, StandHeight / 2f, 0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        float h = 0f, f = 0f;
        bool sprint = false, jumpPressed = false, slidePressed = false;
        var kb = Keyboard.current;
        if (InputEnabled && kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) f += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) f -= 1f;
            sprint = kb.leftShiftKey.isPressed;
            jumpPressed = kb.spaceKey.wasPressedThisFrame;
            slidePressed = kb.leftCtrlKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
        }
        if (autoRun && InputEnabled && f >= 0f) f = 1f;

        // --- ground state ---
        bool wasGrounded = IsGrounded;
        IsGrounded = cc.isGrounded;
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
            if (!wasGrounded && airTime > 0.15f) Landed?.Invoke();
            airTime = 0f;
        }
        else airTime += dt;

        ridingPlatform = IsGrounded ? FindPlatform() : null;

        // --- vertical ---
        if (IsGrounded && velocity.y < 0f) velocity.y = -2f;
        else velocity.y = Mathf.Max(velocity.y + gravity * dt, maxFallSpeed);
        if ((cc.collisionFlags & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;

        // --- jump (buffered + coyote) ---
        if (jumpPressed) lastJumpPressed = Time.time;
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        if (Time.time - lastJumpPressed <= jumpBufferTime && canJump && (!IsSliding || CanStand()))
        {
            if (IsSliding) EndSlide();
            velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight);
            lastJumpPressed = -10f;
            lastGroundedTime = -10f;
            Jumped?.Invoke();
        }

        // --- slide ---
        if (slidePressed && IsGrounded && !IsSliding && Time.time >= slideReady) StartSlide();
        if (IsSliding && Time.time >= slideEnd && CanStand()) EndSlide();

        // --- horizontal ---
        float fwd = f > 0f ? (sprint ? sprintSpeed : runSpeed) : (f < 0f ? -backSpeed : 0f);
        IsSprinting = sprint && f > 0f && !IsSliding;
        Vector3 target;
        float accel;
        if (IsSliding)
        {
            float k = Mathf.Clamp01((slideEnd - Time.time) / slideDuration);
            target = new Vector3(h * strafeSpeed * 0.4f, 0f, slideSpeed * Mathf.Lerp(0.7f, 1f, k));
            accel = 90f;
        }
        else
        {
            target = new Vector3(h * strafeSpeed, 0f, fwd);
            accel = IsGrounded ? groundAcceleration : airAcceleration;
        }
        velocity.x = Mathf.MoveTowards(velocity.x, target.x, accel * dt);
        velocity.z = Mathf.MoveTowards(velocity.z, target.z, accel * dt);

        external = Vector3.MoveTowards(external, Vector3.zero, 25f * dt);
        Vector3 move = (velocity + external) * dt;
        if (ridingPlatform != null) move += ridingPlatform.Delta;
        cc.Move(move);
    }

    /// <summary>What is under the feet: moving platforms carry the player, fragile platforms start their warning.</summary>
    MovingPlatform FindPlatform()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 0.7f, ~0, QueryTriggerInteraction.Ignore))
        {
            var dp = hit.collider.GetComponentInParent<DisappearingPlatform>();
            if (dp != null) dp.NotifyStepped();
            return hit.collider.GetComponentInParent<MovingPlatform>();
        }
        return null;
    }

    void StartSlide()
    {
        IsSliding = true;
        slideEnd = Time.time + slideDuration;
        cc.height = SlideHeight;
        cc.center = new Vector3(0f, SlideHeight / 2f, 0f);
        SlideStarted?.Invoke();
    }

    void EndSlide()
    {
        IsSliding = false;
        slideReady = Time.time + slideCooldown;
        cc.height = StandHeight;
        cc.center = new Vector3(0f, StandHeight / 2f, 0f);
    }

    /// <summary>True when there is headroom to stand up (checks the upper half of the standing capsule).</summary>
    bool CanStand()
    {
        Vector3 p = transform.position;
        float r = cc.radius;
        int n = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * (SlideHeight + r), p + Vector3.up * (StandHeight - r),
            r * 0.95f, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
            if (overlapBuffer[i] != cc) return false;
        return true;
    }

    public void Knockback(Vector3 v) { external = v; }

    public void Teleport(Vector3 position)
    {
        cc.enabled = false;
        transform.position = position;
        cc.enabled = true;
        velocity = Vector3.zero;
        external = Vector3.zero;
        airTime = 0f;
        if (IsSliding) EndSlide();
        foreach (var tr in GetComponentsInChildren<TrailRenderer>()) tr.Clear();
    }
}
