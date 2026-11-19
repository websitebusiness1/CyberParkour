using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum LevelState { Playing, Paused, Failed, Complete, Challenge }

public class LevelResult
{
    public int coins, bonus, tokens, tokensTotal, challengesPassed, quizMistakes;
    public float time;
    public bool objectiveComplete, beatPar;
    public int Total => coins + bonus;
}

/// <summary>
/// Owns the level SESSION state (timer, run coins, checkpoints, pause/fail/complete).
/// Nothing here is permanent progression - saving to the database arrives in Phase 4.
/// Rule for run coins: coins picked up AFTER the last checkpoint are forfeited (and respawn) when you
/// fall or retry from a checkpoint. Coins before the checkpoint are kept.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public LevelConfig config;
    public PlayerController player;
    public PlayerHealth health;
    public Transform finishPoint;

    public LevelState State { get; private set; } = LevelState.Playing;
    public float ElapsedTime { get; private set; }
    public int RunCoins { get; private set; }
    public int TokensCollected { get; private set; }
    public int TokensTotal { get; private set; }
    public int CheckpointsReached { get; private set; }
    public int ChallengesPassed { get; private set; }
    public int QuizMistakes { get; private set; }
    public ChallengeTrigger ActiveChallenge { get; private set; }
    public LevelResult Result { get; private set; }

    public event Action StateChanged;
    public event Action Respawned;
    public event Action CheckpointActivated;
    public event Action<string, float> Message;

    readonly List<CoinCollectible> sinceCheckpoint = new List<CoinCollectible>();
    Vector3 checkpointPosition;
    float startZ, finishZ;
    PlayerAnimationController anim;

    public float Progress =>
        Mathf.Clamp01((player.transform.position.z - startZ) / Mathf.Max(1f, finishZ - startZ));

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        if (config == null || player == null || health == null)
        {
            Debug.LogError("LevelManager is missing config/player/health references.");
            enabled = false;
            return;
        }
        anim = player.GetComponent<PlayerAnimationController>();
        checkpointPosition = player.transform.position;
        startZ = checkpointPosition.z;
        finishZ = finishPoint != null ? finishPoint.position.z : startZ + 100f;

        foreach (var c in FindObjectsByType<CoinCollectible>())
            if (c.isSecurityToken) TokensTotal++;

        health.Init(config.maxLives);
        health.Died += OnPlayerDied;
        health.Damaged += OnPlayerDamaged;
        player.Jumped += OnJumped;
        player.Landed += OnLanded;
        player.SlideStarted += OnSlide;
    }

    void OnDestroy()
    {
        if (health != null) { health.Died -= OnPlayerDied; health.Damaged -= OnPlayerDamaged; }
        if (player != null) { player.Jumped -= OnJumped; player.Landed -= OnLanded; player.SlideStarted -= OnSlide; }
        if (Instance == this) Instance = null;
        Time.timeScale = 1f;
    }

    void Update()
    {
        if (State == LevelState.Playing)
        {
            ElapsedTime += Time.deltaTime;
            if (player.transform.position.y < config.killY) OnFell();
        }

        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (State == LevelState.Playing) Pause();
            else if (State == LevelState.Paused) Resume();
        }
    }

    // ---------- audio hooks ----------
    void OnJumped() { AudioManager.Play(Sfx.Jump); }
    void OnLanded() { AudioManager.Play(Sfx.Land); }
    void OnSlide() { AudioManager.Play(Sfx.Slide); }
    void OnPlayerDamaged() { AudioManager.Play(Sfx.Hit); }

    // ---------- coins ----------
    int ValueOf(CoinCollectible c) => c.isSecurityToken ? config.tokenValue : config.coinValue;

    public void OnCoinCollected(CoinCollectible c)
    {
        if (State != LevelState.Playing) return;
        RunCoins += ValueOf(c);
        if (c.isSecurityToken) TokensCollected++;
        sinceCheckpoint.Add(c);
        AudioManager.Play(c.isSecurityToken ? Sfx.Token : Sfx.Coin);
    }

    void ForfeitCoinsSinceCheckpoint()
    {
        foreach (var c in sinceCheckpoint)
        {
            RunCoins -= ValueOf(c);
            if (c.isSecurityToken) TokensCollected--;
            c.Restore();
        }
        sinceCheckpoint.Clear();
    }

    // ---------- checkpoints / respawn ----------
    public void OnCheckpointReached(Checkpoint cp)
    {
        CheckpointsReached++;
        checkpointPosition = cp.RespawnPosition;
        sinceCheckpoint.Clear(); // coins collected so far are now safe for this run
        AudioManager.Play(Sfx.Checkpoint);
        ShowMessage("CHECKPOINT SAVED", 2f);
        CheckpointActivated?.Invoke();
    }

    void OnFell()
    {
        health.TakeDamage(1, true); // falling always costs a life
        if (health.IsDead) return;  // Died event -> Fail()
        Respawn();
    }

    void Respawn(string extra = null)
    {
        ForfeitCoinsSinceCheckpoint();
        CoverZone.ResetAll();
        player.Teleport(checkpointPosition);
        health.GrantInvulnerability(2f);
        Respawned?.Invoke();
        ShowMessage((string.IsNullOrEmpty(extra) ? "" : extra + "\n") + "Respawned - " + health.Lives + " lives left", 4f);
    }

    /// <summary>Costs a life and sends the player back to the last checkpoint (chaser wall, fake gateway, overload).</summary>
    public void HitAndRespawn(string message)
    {
        if (State != LevelState.Playing) return;
        if (!health.TakeDamage(1)) return;
        if (health.IsDead) return;   // Died event -> Fail()
        Respawn(message);
    }

    // ---------- security challenges (quiz gates) ----------
    public void BeginChallenge(ChallengeTrigger c)
    {
        if (State != LevelState.Playing) return;
        ActiveChallenge = c;
        State = LevelState.Challenge;
        Time.timeScale = 0f;
        StateChanged?.Invoke();
    }

    public void RegisterWrongAnswer() { QuizMistakes++; }

    public void FinishChallenge()
    {
        if (State != LevelState.Challenge || ActiveChallenge == null) return;
        ActiveChallenge.MarkPassed();
        ActiveChallenge = null;
        ChallengesPassed++;
        State = LevelState.Playing;
        Time.timeScale = 1f;
        AudioManager.Play(Sfx.Checkpoint);
        StateChanged?.Invoke();
        ShowMessage("SECURITY CHECK PASSED - the gate is open", 3f);
    }

    void OnPlayerDied()
    {
        if (State != LevelState.Playing) return;
        State = LevelState.Failed;
        player.InputEnabled = false;
        AudioManager.Play(Sfx.Fail);
        StateChanged?.Invoke();
    }

    public bool CanRetryCheckpoint => State == LevelState.Failed && CheckpointsReached > 0;

    public void RetryFromCheckpoint()
    {
        if (!CanRetryCheckpoint) return;
        health.ResetFull();
        anim?.ResetState();
        ForfeitCoinsSinceCheckpoint();
        CoverZone.ResetAll();
        player.Teleport(checkpointPosition);
        Respawned?.Invoke();
        player.InputEnabled = true;
        State = LevelState.Playing;
        StateChanged?.Invoke();
        ShowMessage("Retrying from checkpoint", 2f);
    }

    // ---------- completion ----------
    public void TryComplete()
    {
        if (State != LevelState.Playing) return;
        if (CheckpointsReached < config.requiredCheckpoints)
        {
            ShowMessage("Reach the checkpoint before the gateway!", 3f);
            return;
        }
        if (ChallengesPassed < config.requiredChallenges)
        {
            ShowMessage("Complete the security check first!", 3f);
            return;
        }
        if (TokensCollected < config.requiredTokens)
        {
            ShowMessage("Collect " + config.requiredTokens + " security tokens first", 3f);
            return;
        }

        State = LevelState.Complete;
        player.InputEnabled = false;
        anim?.PlayVictory();
        Result = new LevelResult
        {
            coins = RunCoins,
            bonus = config.completionBonus,
            time = ElapsedTime,
            tokens = TokensCollected,
            tokensTotal = TokensTotal,
            objectiveComplete = true,
            challengesPassed = ChallengesPassed,
            quizMistakes = QuizMistakes,
            beatPar = ElapsedTime <= config.parTimeSeconds
        };
        AudioManager.Play(Sfx.Complete);
        StateChanged?.Invoke();
    }

    // ---------- pause / flow ----------
    public void Pause()
    {
        if (State != LevelState.Playing) return;
        State = LevelState.Paused;
        Time.timeScale = 0f;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (State != LevelState.Paused) return;
        State = LevelState.Playing;
        Time.timeScale = 1f;
        StateChanged?.Invoke();
    }

    public void ShowMessage(string text, float duration = 3f) { Message?.Invoke(text, duration); }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded("MainMenu"))
        {
            ShowMessage("MainMenu scene not in Build Settings", 3f);
            return;
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public bool HasNextLevel => !string.IsNullOrEmpty(config.nextSceneName) && Application.CanStreamedLevelBeLoaded(config.nextSceneName);

    public void LoadNextLevel()
    {
        if (!HasNextLevel) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(config.nextSceneName);
    }
}
