using System;
using UnityEngine;

/// <summary>Lives + brief invulnerability. Rule: each hit or fall costs 1 life; 0 lives = level failed.</summary>
public class PlayerHealth : MonoBehaviour
{
    public int maxLives = 3;
    public float damageInvulnerability = 1.5f;

    public int Lives { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsInvulnerable => Time.time < invulnerableUntil;

    public event Action<int> LivesChanged;
    public event Action Damaged;
    public event Action Died;

    float invulnerableUntil;

    void Awake() { Lives = maxLives; }

    public void Init(int max)
    {
        maxLives = max;
        Lives = max;
        IsDead = false;
        LivesChanged?.Invoke(Lives);
    }

    public bool TakeDamage(int amount, bool ignoreInvulnerability = false)
    {
        if (IsDead) return false;
        if (!ignoreInvulnerability && IsInvulnerable) return false;
        Lives = Mathf.Max(0, Lives - amount);
        invulnerableUntil = Time.time + damageInvulnerability;
        LivesChanged?.Invoke(Lives);
        if (Lives == 0) { IsDead = true; Died?.Invoke(); }
        else Damaged?.Invoke();
        return true;
    }

    public void GrantInvulnerability(float seconds)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }

    public void ResetFull()
    {
        Init(maxLives);
        invulnerableUntil = Time.time + 2f;
    }
}
