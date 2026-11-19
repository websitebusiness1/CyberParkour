using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>In-game HUD, pause menu, confirmation dialog, results screen (with database save status), game-over screen.
/// Built at runtime from UIFactory. Reads LevelManager state; reward saving goes through PlayerProgressionManager.</summary>
public class UIManager : MonoBehaviour
{
    LevelManager lm;
    Canvas canvas;

    Text coinText, tokenText, bankedText, titleText, objectiveText, timeText, checkpointText, progressText, toastText;
    RectTransform progressFill;
    GameObject pausePanel, confirmPanel, resultsPanel, gameOverPanel;
    Text confirmLabel, resultsBody, saveStatusText;
    Button resumeBtn, confirmNoBtn, nextBtn, replayBtn, retrySaveBtn, retryCheckpointBtn, restartBtn;
    Action confirmAction;
    Coroutine toastRoutine;
    readonly List<Image> lifeIcons = new List<Image>();

    // security challenge (quiz) panel
    GameObject challengePanel;
    Text challengeTitle, challengeQuestion, challengeFeedback, resultsTitle;
    Button[] challengeBtns = new Button[4];
    Text[] challengeLabels = new Text[4];
    Button challengeContinue;
    ChallengeData challengeData;
    bool challengeSolved;

    // corruption distance gauge (ransomware level)
    Text corruptionText;
    RectTransform corruptionFill;
    Image corruptionFillImg;

    // network overload bar (DDoS level)
    RectTransform overloadFill;
    Image overloadFillImg;

    PendingCompletion pending;
    bool saving;
    int lastCoins = -1, lastTokens = -1, lastLives = -1, lastCheckpoints = -1, lastTenth = -1, lastPercent = -1, lastBanked = -2;

    void Start()
    {
        lm = LevelManager.Instance;
        if (lm == null) { Debug.LogError("UIManager: no LevelManager in scene."); enabled = false; return; }

        UIFactory.EnsureEventSystem();
        canvas = UIFactory.CreateCanvas("GameCanvas", 10);
        BuildHud();
        BuildPause();
        BuildConfirm();
        BuildResults();
        BuildGameOver();
        BuildChallenge();

        lm.StateChanged += OnStateChanged;
        lm.Message += ShowToast;

        var c = lm.config;
        ShowToast("LEVEL " + c.levelNumber + ": " + c.threatName.ToUpper() + " - " + c.levelTitle +
                  "\n" + c.objective + "\nWASD move | SPACE jump | SHIFT sprint | CTRL or C slide | ESC pause", 7f);
    }

    void OnDestroy()
    {
        if (lm != null) { lm.StateChanged -= OnStateChanged; lm.Message -= ShowToast; }
    }

    // ---------------- HUD ----------------
    Text HudLabel(string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta, Transform parent)
    {
        var t = UIFactory.Label(parent, text, size, color, align);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        return t;
    }

    void BuildHud()
    {
        var hud = UIFactory.NewUI("HUD", canvas.transform);
        UIFactory.Stretch(hud.GetComponent<RectTransform>());
        var p = hud.transform;
        var c = lm.config;
        Vector2 TL = new Vector2(0, 1), TC = new Vector2(0.5f, 1), TR = new Vector2(1, 1);

        // top-left: coins / tokens / banked
        var left = UIFactory.Plate(p, "StatsPlate", TL, TL, new Vector2(20, -20), new Vector2(470, 150), UIFactory.Gold);
        coinText = HudLabel("RUN COINS 0", 38, UIFactory.Gold, TextAnchor.UpperLeft, TL, TL, new Vector2(24, -10), new Vector2(430, 46), left);
        tokenText = HudLabel("TOKENS 0/" + lm.TokensTotal, 28, UIFactory.Green, TextAnchor.UpperLeft, TL, TL, new Vector2(24, -62), new Vector2(430, 36), left);
        bankedText = HudLabel("", 22, UIFactory.Dim, TextAnchor.UpperLeft, TL, TL, new Vector2(24, -104), new Vector2(440, 32), left);

        // top-centre: level + objective
        var mid = UIFactory.Plate(p, "LevelPlate", TC, TC, new Vector2(0, -20), new Vector2(1100, 94), UIFactory.Cyan);
        titleText = HudLabel("LEVEL " + c.levelNumber + " | " + c.threatName.ToUpper(), 34, UIFactory.Cyan, TextAnchor.UpperCenter, TC, TC, new Vector2(0, -8), new Vector2(1040, 42), mid);
        objectiveText = HudLabel("OBJECTIVE: " + c.objective, 22, UIFactory.White, TextAnchor.UpperCenter, TC, TC, new Vector2(0, -54), new Vector2(1040, 32), mid);

        // top-right: lives (diamond icons) + time
        var right = UIFactory.Plate(p, "LivesPlate", TR, TR, new Vector2(-20, -20), new Vector2(380, 110), UIFactory.Red);
        HudLabel("LIVES", 28, UIFactory.Red, TextAnchor.UpperLeft, TL, TL, new Vector2(22, -14), new Vector2(110, 36), right);
        lifeIcons.Clear();
        for (int i = 0; i < c.maxLives; i++)
        {
            var icon = UIFactory.NewUI("Life" + i, right);
            var rt = icon.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = TL;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(150 + i * 46, -30);
            rt.sizeDelta = new Vector2(28, 28);
            rt.localRotation = Quaternion.Euler(0, 0, 45f);
            var img = icon.AddComponent<Image>();
            img.raycastTarget = false;
            lifeIcons.Add(img);
        }
        timeText = HudLabel("TIME 0:00.0", 30, UIFactory.White, TextAnchor.UpperLeft, TL, TL, new Vector2(22, -64), new Vector2(340, 38), right);

        // bottom-centre: progress to the gateway
        var prog = UIFactory.Plate(p, "ProgressPlate", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(980, 70), UIFactory.Cyan);
        progressText = HudLabel("GATEWAY 0%", 22, UIFactory.Cyan, TextAnchor.UpperCenter, TC, TC, new Vector2(0, -6), new Vector2(600, 28), prog);
        var bg = UIFactory.NewUI("ProgressBG", prog);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.6f);
        bgImg.raycastTarget = false;
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0.03f, 0f);
        bgRt.anchorMax = new Vector2(0.97f, 0f);
        bgRt.pivot = new Vector2(0.5f, 0f);
        bgRt.anchoredPosition = new Vector2(0, 12);
        bgRt.sizeDelta = new Vector2(0, 16);
        var fill = UIFactory.NewUI("Fill", bg.transform);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = UIFactory.Cyan;
        fillImg.raycastTarget = false;
        progressFill = fill.GetComponent<RectTransform>();
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;

        // bottom corners
        var cpPlate = UIFactory.Plate(p, "CheckpointPlate", new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(360, 52), UIFactory.Amber);
        checkpointText = HudLabel("CHECKPOINT 0/" + c.requiredCheckpoints, 24, UIFactory.Amber, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(330, 40), cpPlate);
        var pausePlate = UIFactory.Plate(p, "PausePlate", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(250, 52), UIFactory.Dim);
        HudLabel("[ESC] PAUSE", 24, UIFactory.White, TextAnchor.MiddleLeft, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(220, 40), pausePlate);

        if (OverloadMeter.Instance != null)
        {
            var op = UIFactory.Plate(p, "OverloadPlate", TL, TL, new Vector2(20, -180), new Vector2(470, 76), UIFactory.Amber);
            HudLabel("NETWORK LOAD", 22, UIFactory.Amber, TextAnchor.UpperLeft, TL, TL, new Vector2(24, -6), new Vector2(300, 28), op);
            var obg = UIFactory.NewUI("OverloadBG", op);
            var obgImg = obg.AddComponent<Image>();
            obgImg.color = new Color(0f, 0f, 0f, 0.6f);
            obgImg.raycastTarget = false;
            var obr = obg.GetComponent<RectTransform>();
            obr.anchorMin = new Vector2(0.05f, 0f);
            obr.anchorMax = new Vector2(0.97f, 0f);
            obr.pivot = new Vector2(0.5f, 0f);
            obr.anchoredPosition = new Vector2(0, 12);
            obr.sizeDelta = new Vector2(0, 22);
            var of = UIFactory.NewUI("Fill", obg.transform);
            overloadFillImg = of.AddComponent<Image>();
            overloadFillImg.raycastTarget = false;
            overloadFill = of.GetComponent<RectTransform>();
            overloadFill.anchorMin = Vector2.zero;
            overloadFill.anchorMax = new Vector2(0f, 1f);
            overloadFill.offsetMin = overloadFill.offsetMax = Vector2.zero;
        }

        if (ChasingCorruption.Instance != null)
        {
            var cpl = UIFactory.Plate(p, "CorruptionPlate", TL, TL, new Vector2(20, -180), new Vector2(470, 76), UIFactory.Red);
            corruptionText = HudLabel("CORRUPTION BEHIND", 22, UIFactory.Red, TextAnchor.UpperLeft, TL, TL, new Vector2(24, -6), new Vector2(430, 28), cpl);
            var cbg = UIFactory.NewUI("CorruptionBG", cpl);
            var cbgImg = cbg.AddComponent<Image>();
            cbgImg.color = new Color(0f, 0f, 0f, 0.6f);
            cbgImg.raycastTarget = false;
            var cbr = cbg.GetComponent<RectTransform>();
            cbr.anchorMin = new Vector2(0.05f, 0f);
            cbr.anchorMax = new Vector2(0.97f, 0f);
            cbr.pivot = new Vector2(0.5f, 0f);
            cbr.anchoredPosition = new Vector2(0, 12);
            cbr.sizeDelta = new Vector2(0, 22);
            var cf = UIFactory.NewUI("Fill", cbg.transform);
            corruptionFillImg = cf.AddComponent<Image>();
            corruptionFillImg.raycastTarget = false;
            corruptionFill = cf.GetComponent<RectTransform>();
            corruptionFill.anchorMin = Vector2.zero;
            corruptionFill.anchorMax = new Vector2(0f, 1f);
            corruptionFill.offsetMin = corruptionFill.offsetMax = Vector2.zero;
        }

        toastText = HudLabel("", 30, UIFactory.Amber, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300, 200), p);
        toastText.gameObject.SetActive(false);
    }

    void UpdateLives(int lives)
    {
        for (int i = 0; i < lifeIcons.Count; i++)
            lifeIcons[i].color = i < lives ? UIFactory.Red : new Color(0.25f, 0.1f, 0.12f, 0.8f);
    }

    void Update()
    {
        if (lm == null) return;
        if (lm.RunCoins != lastCoins) { lastCoins = lm.RunCoins; coinText.text = "RUN COINS " + lastCoins; }
        if (lm.TokensCollected != lastTokens) { lastTokens = lm.TokensCollected; tokenText.text = "TOKENS " + lastTokens + "/" + lm.TokensTotal; }
        if (lm.health.Lives != lastLives) { lastLives = lm.health.Lives; UpdateLives(lastLives); }
        if (lm.CheckpointsReached != lastCheckpoints) { lastCheckpoints = lm.CheckpointsReached; checkpointText.text = "CHECKPOINT " + lastCheckpoints + "/" + lm.config.requiredCheckpoints; }
        int tenth = (int)(lm.ElapsedTime * 10f);
        if (tenth != lastTenth) { lastTenth = tenth; timeText.text = "TIME " + UIFactory.FormatTime(lm.ElapsedTime); }
        float p = lm.Progress;
        int pct = Mathf.RoundToInt(p * 100f);
        if (pct != lastPercent) { lastPercent = pct; progressText.text = "GATEWAY " + pct + "%"; }
        progressFill.anchorMax = new Vector2(p, 1f);

        if (corruptionFill != null && ChasingCorruption.Instance != null)
        {
            var ch = ChasingCorruption.Instance;
            float d = ch.DistanceBehind;
            float danger = ch.Started ? 1f - Mathf.Clamp01(d / 45f) : 0f;
            corruptionFill.anchorMax = new Vector2(danger, 1f);
            corruptionFillImg.color = Color.Lerp(UIFactory.Cyan, UIFactory.Red, danger);
            corruptionText.text = ch.Started ? "CORRUPTION " + Mathf.Max(0, Mathf.RoundToInt(d)) + "m BEHIND" : "CORRUPTION - NOT YET AWAKE";
        }

        if (overloadFill != null && OverloadMeter.Instance != null)
        {
            float ov = OverloadMeter.Instance.Value;
            overloadFill.anchorMax = new Vector2(ov, 1f);
            overloadFillImg.color = Color.Lerp(UIFactory.Cyan, UIFactory.Red, ov);
        }

        var pm = PlayerProgressionManager.Instance;
        int banked = pm != null && pm.HasProfile ? pm.Profile.player.total_coins : -1;
        if (banked != lastBanked)
        {
            lastBanked = banked;
            bankedText.text = banked >= 0 ? "BANKED " + banked + " (database)" : "OFFLINE - rewards not saved";
            bankedText.color = banked >= 0 ? UIFactory.Dim : UIFactory.Amber;
        }
    }

    void ShowToast(string text, float duration)
    {
        if (toastText == null) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastRoutine(text, duration));
    }

    IEnumerator ToastRoutine(string text, float duration)
    {
        toastText.text = text;
        toastText.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(duration);
        toastText.gameObject.SetActive(false);
        toastRoutine = null;
    }

    // ---------------- Pause + confirm ----------------
    void BuildPause()
    {
        var col = UIFactory.MakeColumnPanel(canvas.transform, "PausePanel", out pausePanel);
        UIFactory.Label(col, "PAUSED", 72, UIFactory.Cyan, TextAnchor.MiddleCenter, -1, 110);
        resumeBtn = UIFactory.MakeButton(col, "RESUME", () => lm.Resume());
        UIFactory.MakeButton(col, "RESTART LEVEL", () => Ask("Restart the level?\nProgress in this run will be lost.", lm.RestartLevel));
        UIFactory.MakeButton(col, "RETURN TO MAIN MENU", () => Ask("Abandon this level and return to the menu?\nProgress in this run will be lost.", lm.ReturnToMenu));
        UIFactory.MakeButton(col, "SETTINGS (LATER PHASE)", null, false);
        pausePanel.SetActive(false);
    }

    void BuildConfirm()
    {
        var col = UIFactory.MakeColumnPanel(canvas.transform, "ConfirmPanel", out confirmPanel, 20f);
        confirmLabel = UIFactory.Label(col, "", 38, UIFactory.White, TextAnchor.MiddleCenter, 1000, 160);
        confirmNoBtn = UIFactory.MakeButton(col, "NO, GO BACK", () =>
        {
            confirmPanel.SetActive(false);
            pausePanel.SetActive(true);
            UIFactory.Focus(resumeBtn);
        });
        UIFactory.MakeButton(col, "YES", () => { var a = confirmAction; confirmAction = null; a?.Invoke(); });
        confirmPanel.SetActive(false);
    }

    void Ask(string message, Action onYes)
    {
        confirmAction = onYes;
        confirmLabel.text = message;
        pausePanel.SetActive(false);
        confirmPanel.SetActive(true);
        UIFactory.Focus(confirmNoBtn);
    }

    // ---------------- Results + game over ----------------
    void BuildResults()
    {
        var col = UIFactory.MakeColumnPanel(canvas.transform, "ResultsPanel", out resultsPanel, 8f);
        resultsTitle = UIFactory.Label(col, "MISSION COMPLETE", 72, UIFactory.Green, TextAnchor.MiddleCenter, -1, 100);
        resultsBody = UIFactory.Label(col, "", 24, UIFactory.White, TextAnchor.MiddleCenter, 1100, 400);
        saveStatusText = UIFactory.Label(col, "", 28, UIFactory.Amber, TextAnchor.MiddleCenter, 1100, 100);
        retrySaveBtn = UIFactory.MakeButton(col, "RETRY SAVING", () => StartSave());
        nextBtn = UIFactory.MakeButton(col, "NEXT LEVEL", () => lm.LoadNextLevel());
        replayBtn = UIFactory.MakeButton(col, "REPLAY LEVEL", () => lm.RestartLevel());
        UIFactory.MakeButton(col, "RETURN TO MENU", () => lm.ReturnToMenu());
        retrySaveBtn.gameObject.SetActive(false);
        resultsPanel.SetActive(false);
    }

    void BuildGameOver()
    {
        var col = UIFactory.MakeColumnPanel(canvas.transform, "GameOverPanel", out gameOverPanel, 14f);
        UIFactory.Label(col, "SYSTEM FAILURE", 72, UIFactory.Red, TextAnchor.MiddleCenter, -1, 100);
        UIFactory.Label(col, "The runner was deleted. Out of lives.", 30, UIFactory.White, TextAnchor.MiddleCenter, 1000, 60);
        retryCheckpointBtn = UIFactory.MakeButton(col, "RETRY FROM CHECKPOINT", () => lm.RetryFromCheckpoint());
        restartBtn = UIFactory.MakeButton(col, "RESTART LEVEL", () => lm.RestartLevel());
        UIFactory.MakeButton(col, "RETURN TO MENU", () => lm.ReturnToMenu());
        gameOverPanel.SetActive(false);
    }

    void OnStateChanged()
    {
        pausePanel.SetActive(false);
        confirmPanel.SetActive(false);
        resultsPanel.SetActive(false);
        gameOverPanel.SetActive(false);
        challengePanel.SetActive(false);

        switch (lm.State)
        {
            case LevelState.Challenge:
                ShowChallenge();
                break;
            case LevelState.Paused:
                pausePanel.SetActive(true);
                UIFactory.Focus(resumeBtn);
                break;
            case LevelState.Failed:
                gameOverPanel.SetActive(true);
                retryCheckpointBtn.gameObject.SetActive(lm.CanRetryCheckpoint);
                UIFactory.Focus(lm.CanRetryCheckpoint ? retryCheckpointBtn : restartBtn);
                break;
            case LevelState.Complete:
                FillResults();
                resultsPanel.SetActive(true);
                nextBtn.interactable = lm.HasNextLevel;
                nextBtn.gameObject.SetActive(!lm.config.isFinalLevel);
                resultsTitle.text = lm.config.isFinalLevel ? "SYSTEM RESTORED!" : "MISSION COMPLETE";
                UIFactory.Focus(lm.HasNextLevel ? nextBtn : replayBtn);

                var pm = PlayerProgressionManager.Instance;
                var r = lm.Result;
                pending = pm != null
                    ? pm.NewCompletion(lm.config.levelNumber, r.time, r.coins, r.tokens)
                    : new PendingCompletion { level = lm.config.levelNumber, time = r.time, coins = r.coins, tokens = r.tokens, requestId = Guid.NewGuid().ToString() };
                StartSave();
                break;
        }
    }

    void FillResults()
    {
        var r = lm.Result;
        var c = lm.config;
        string par = r.beatPar ? "  (under par " + UIFactory.FormatTime(c.parTimeSeconds) + ")" : "  (par " + UIFactory.FormatTime(c.parTimeSeconds) + ")";
        resultsBody.text =
            "Threat resolved: " + c.threatName.ToUpper() + " - " + c.levelTitle + "\n" +
            "Coins collected: " + r.coins + "     Completion bonus: +" + r.bonus + "\n" +
            "Completion time: " + UIFactory.FormatTime(r.time) + par + "\n" +
            "Security tokens: " + r.tokens + "/" + r.tokensTotal + "     Objective: COMPLETE\n" +
            (c.requiredChallenges > 0 ? "Security check: PASSED (" + r.quizMistakes + " wrong " + (r.quizMistakes == 1 ? "answer" : "answers") + ")\n" : "") +
            (c.isFinalLevel ? "THE CPU CORE IS RESTORED! Next: take the POST-TEST in the main menu.\n" : "") + "\n" +
            "WHAT YOU LEARNED\n" + c.learningSummary + "\n" + c.learningTip;
    }

    /// <summary>Sends the completion to the database. Shows LOCAL claim vs SERVER-CONFIRMED result separately.</summary>
    async void StartSave()
    {
        if (saving || pending == null) return;
        saving = true;
        retrySaveBtn.gameObject.SetActive(false);
        saveStatusText.color = UIFactory.Amber;
        saveStatusText.text = "Saving to database...  (run total claimed: " + (lm.Result.coins + lm.Result.bonus) + ")";

        var pm = PlayerProgressionManager.Instance;
        OpResult res = pm == null
            ? OpResult.Fail("offline", "Offline run - reward NOT saved. Sign in from the main menu to bank coins.")
            : await pm.SubmitCompletion(pending);

        saving = false;
        if (this == null || lm == null) return;

        if (res.Ok)
        {
            var resp = res.Response;
            string text = "SAVED TO DATABASE   +" + resp.reward + " coins";
            if (resp.duplicate) text = "ALREADY SAVED (duplicate request ignored - no extra coins)";
            else if (resp.reward == 0) text += "\n(Replay: only coins above your previous best are paid)";
            text += "\nBanked total: " + pm.Profile.player.total_coins;
            if (resp.newly_unlocked_level > 0) text += "\nLEVEL " + resp.newly_unlocked_level + " UNLOCKED!";
            saveStatusText.color = UIFactory.Green;
            saveStatusText.text = text;
            pending = null;
        }
        else
        {
            bool offline = res.Code == "offline";
            saveStatusText.color = offline ? UIFactory.Amber : UIFactory.Red;
            saveStatusText.text = (offline ? "" : "NOT SAVED: ") + res.Message;
            if (res.Retryable)
            {
                retrySaveBtn.gameObject.SetActive(true);
                UIFactory.Focus(retrySaveBtn);
            }
            else pending = null;
        }
    }

    // ---------------- Security challenge (quiz gate) ----------------
    void BuildChallenge()
    {
        var col = UIFactory.MakeColumnPanel(canvas.transform, "ChallengePanel", out challengePanel, 10f, 0.93f);
        challengeTitle = UIFactory.Label(col, "SECURITY CHECK", 56, UIFactory.Cyan, TextAnchor.MiddleCenter, -1, 80);
        challengeQuestion = UIFactory.Label(col, "", 30, UIFactory.White, TextAnchor.MiddleCenter, 1150, 170);
        for (int i = 0; i < challengeBtns.Length; i++)
        {
            int idx = i;
            challengeBtns[i] = UIFactory.MakeButton(col, "-", () => OnChallengePick(idx), true, 1100, 76);
            challengeLabels[i] = challengeBtns[i].GetComponentInChildren<Text>();
            challengeLabels[i].fontSize = 24;
        }
        challengeFeedback = UIFactory.Label(col, "", 26, UIFactory.Amber, TextAnchor.MiddleCenter, 1150, 130);
        challengeContinue = UIFactory.MakeButton(col, "CONTINUE", () => { challengePanel.SetActive(false); lm.FinishChallenge(); });
        challengePanel.SetActive(false);
    }

    void ShowChallenge()
    {
        var c = lm.ActiveChallenge;
        if (c == null || c.data == null) { lm.FinishChallenge(); return; }
        challengeData = c.data;
        challengeSolved = false;
        challengeTitle.text = challengeData.title;
        challengeQuestion.text = challengeData.question;
        challengeFeedback.text = "Choose the best answer.";
        challengeFeedback.color = UIFactory.Amber;
        for (int i = 0; i < challengeBtns.Length; i++)
        {
            bool has = challengeData.options != null && i < challengeData.options.Length;
            challengeBtns[i].gameObject.SetActive(has);
            if (has)
            {
                challengeLabels[i].text = (char)('A' + i) + ".  " + challengeData.options[i];
                challengeBtns[i].interactable = true;
            }
        }
        challengeContinue.gameObject.SetActive(false);
        challengePanel.SetActive(true);
        UIFactory.Focus(challengeBtns[0]);
    }

    void OnChallengePick(int i)
    {
        if (challengeSolved || challengeData == null) return;
        if (i == challengeData.correctIndex)
        {
            challengeSolved = true;
            challengeFeedback.color = UIFactory.Green;
            challengeFeedback.text = "CORRECT!  " + challengeData.explanation;
            foreach (var b in challengeBtns) b.interactable = false;
            challengeContinue.gameObject.SetActive(true);
            UIFactory.Focus(challengeContinue);
            AudioManager.Play(Sfx.Token);
        }
        else
        {
            lm.RegisterWrongAnswer();
            challengeFeedback.color = UIFactory.Red;
            challengeFeedback.text = "NOT QUITE.  " + challengeData.explanation + "\nTry another answer.";
            challengeBtns[i].interactable = false;
            for (int k = 0; k < challengeBtns.Length; k++)
                if (challengeBtns[k].gameObject.activeSelf && challengeBtns[k].interactable) { UIFactory.Focus(challengeBtns[k]); break; }
            AudioManager.Play(Sfx.Hit);
        }
    }
}
