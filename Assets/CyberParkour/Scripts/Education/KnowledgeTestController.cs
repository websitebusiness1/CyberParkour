using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Pre-test (form A) before the campaign and post-test (form B) after Level 8.
///  - 16 questions (2 per threat). Question order and answer positions are shuffled; the SERVER scores the answers.
///  - Results are saved to the database only with research consent; otherwise they stay on this device.
///  - Pre-test answers are NOT explained (explaining them would teach the post-test); the post-test review explains everything.
/// </summary>
public class KnowledgeTestController : MonoBehaviour
{
    public string menuScene = "MainMenu";
    /// <summary>Set to true if your teacher wants explanations right after the pre-test (weakens the before/after comparison).</summary>
    public bool showExplanationsAfterPre = false;

    class Result { public bool has; public int score, total; public int[] threat = new int[8]; }

    Canvas canvas;
    GameObject panel;
    PlayerProgressionManager pm;

    // current attempt
    string phase, testSet;
    QuizQuestion[] form;
    int[] qOrder;          // display order of questions (indices into form)
    int[][] optOrder;      // display order of options per question
    int[] chosen;          // chosen ORIGINAL option index per form question (-1 = none)
    int shown;             // how many questions have been answered
    int reviewIndex;
    bool lastRemote;
    Text statusText;
    readonly List<Button> busy = new List<Button>();

    bool Online => pm.IsOnline;

    void Start()
    {
        Time.timeScale = 1f;
        UIFactory.EnsureEventSystem();
        pm = PlayerProgressionManager.Ensure();
        canvas = UIFactory.CreateCanvas("TestCanvas");
        ShowIntro();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && form == null) GoBack();
    }

    RectTransform NewPanel()
    {
        if (panel != null) Destroy(panel);
        busy.Clear();
        return UIFactory.MakeColumnPanel(canvas.transform, "TestPanel", out panel, 10f, 0.9f);
    }

    void GoBack() { SceneManager.LoadScene(menuScene); }

    // ---------------- results helpers ----------------
    Result GetResult(string ph)
    {
        var r = new Result();
        if (Online && pm.Profile.quiz != null)
        {
            var q = pm.Profile.quiz;
            bool has = ph == "pre" ? q.has_pre : q.has_post;
            if (has)
            {
                r.has = true;
                r.score = ph == "pre" ? q.pre_score : q.post_score;
                r.total = ph == "pre" ? q.pre_total : q.post_total;
                var t = ph == "pre" ? q.pre_threat : q.post_threat;
                for (int i = 0; i < 8 && t != null && i < t.Length; i++) r.threat[i] = t[i];
                return r;
            }
        }
        var local = LocalQuizStore.Load(ph);
        if (local != null)
        {
            r.has = true; r.score = local.score; r.total = local.total;
            for (int i = 0; i < 8 && local.perThreat != null && i < local.perThreat.Length; i++) r.threat[i] = local.perThreat[i];
        }
        return r;
    }

    bool Level8Done()
    {
        if (!Online || pm.Profile.levels == null) return false;
        foreach (var l in pm.Profile.levels) if (l.id == 8) return l.completed;
        return false;
    }

    // ---------------- intro ----------------
    void ShowIntro()
    {
        form = null;
        var col = NewPanel();
        UIFactory.Label(col, "CYBERSECURITY KNOWLEDGE TEST", 52, UIFactory.Cyan, TextAnchor.MiddleCenter, 1200, 80);
        UIFactory.Label(col, "Take the PRE-TEST before you play the campaign and the POST-TEST after you finish Level 8.\n" +
            "Each test has 16 questions (2 per threat). Comparing them shows how your knowledge changed.", 24, UIFactory.White, TextAnchor.MiddleCenter, 1150, 100);

        var pre = GetResult("pre"); var post = GetResult("post");
        bool l8 = Level8Done();
        UIFactory.Label(col, pre.has ? "PRE-TEST: completed  (" + pre.score + "/" + pre.total + ")" : "PRE-TEST: not taken yet", 28, pre.has ? UIFactory.Green : UIFactory.Amber, TextAnchor.MiddleCenter, 1150, 40);
        string postLine = post.has ? "POST-TEST: completed  (" + post.score + "/" + post.total + ")"
                        : Online ? (l8 ? (pre.has ? "POST-TEST: available now" : "POST-TEST: take the pre-test first") : "POST-TEST: unlocks when you complete Level 8")
                        : "POST-TEST: practice mode (offline)";
        UIFactory.Label(col, postLine, 28, post.has ? UIFactory.Green : (Online && !l8 ? UIFactory.Dim : UIFactory.Amber), TextAnchor.MiddleCenter, 1150, 40);

        string mode = !Online ? "OFFLINE / practice mode: results stay on this device only."
                    : pm.Profile.player.research_consent ? "Saving to the project database: ON (research consent given)."
                    : pm.Profile.player.consent_answered ? "Saving: OFF - results stay on this device only."
                    : "You will be asked for research consent before the first test.";
        UIFactory.Label(col, mode, 22, UIFactory.Dim, TextAnchor.MiddleCenter, 1150, 40);

        bool canPre = !pre.has;
        bool canPost = Online ? (l8 && pre.has && !post.has) : !post.has;
        var bPre = UIFactory.MakeButton(col, "START PRE-TEST", () => Begin("pre"), canPre);
        UIFactory.MakeButton(col, "START POST-TEST", () => Begin("post"), canPost);
        UIFactory.MakeButton(col, "VIEW MY RESULTS", () => ShowResults(null, false), pre.has || post.has);
        UIFactory.MakeButton(col, "RESEARCH CONSENT SETTINGS", () => ShowConsent(null), Online);
        var back = UIFactory.MakeButton(col, "BACK TO MENU", GoBack);
        UIFactory.Focus(canPre ? bPre : back);
    }

    // ---------------- consent ----------------
    void Begin(string ph)
    {
        if (Online && !pm.Profile.player.consent_answered) ShowConsent(ph);
        else StartTest(ph);
    }

    void ShowConsent(string next)
    {
        var col = NewPanel();
        UIFactory.Label(col, "RESEARCH CONSENT", 52, UIFactory.Cyan, TextAnchor.MiddleCenter, 1200, 80);
        UIFactory.Label(col,
            "Cyber Parkour is part of a capstone study on whether a game can help people learn about cybersecurity.\n\n" +
            "If you AGREE, your test answers and scores are saved to the project database, linked only to your anonymous player ID. " +
            "No real name or email appears in the results. The data is used only for the study and reported as group results.\n\n" +
            "If you say NO, you can still take the tests - your results stay on this device only and nothing is sent. " +
            "You may change this later, and you can ask the researcher to delete data saved about you.",
            24, UIFactory.White, TextAnchor.MiddleCenter, 1150, 400);
        statusText = UIFactory.Label(col, pm.Profile != null && pm.Profile.player.consent_answered
            ? "Current choice: " + (pm.Profile.player.research_consent ? "AGREED" : "DECLINED") : "", 24, UIFactory.Amber, TextAnchor.MiddleCenter, 1150, 36);
        var yes = UIFactory.MakeButton(col, "I AGREE - SAVE MY RESULTS", () => SetConsent(true, next));
        var no = UIFactory.MakeButton(col, "NO THANKS - KEEP RESULTS ON THIS DEVICE", () => SetConsent(false, next));
        var back = UIFactory.MakeButton(col, "BACK", ShowIntro);
        busy.AddRange(new[] { yes, no, back });
        UIFactory.Focus(yes);
    }

    async void SetConsent(bool agree, string next)
    {
        foreach (var b in busy) if (b != null) b.interactable = false;
        statusText.color = UIFactory.Amber;
        statusText.text = "Saving your choice...";
        var res = await pm.SetResearchConsent(agree);
        if (this == null) return;
        if (!res.Ok)
        {
            statusText.color = UIFactory.Red;
            statusText.text = res.Message;
            foreach (var b in busy) if (b != null) b.interactable = true;
            return;
        }
        if (next != null) StartTest(next); else ShowIntro();
    }

    // ---------------- the test ----------------
    void StartTest(string ph)
    {
        phase = ph;
        testSet = ph == "pre" ? "A" : "B";
        form = QuestionBank.Form(testSet);
        int n = form.Length;
        chosen = new int[n];
        for (int i = 0; i < n; i++) chosen[i] = -1;
        qOrder = Shuffled(n);
        optOrder = new int[n][];
        for (int i = 0; i < n; i++) optOrder[i] = Shuffled(form[i].options.Length);
        shown = 0;
        ShowQuestion();
    }

    static int[] Shuffled(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    void ShowQuestion()
    {
        var col = NewPanel();
        int qi = qOrder[shown];
        var q = form[qi];
        UIFactory.Label(col, (phase == "pre" ? "PRE-TEST" : "POST-TEST") + "   -   QUESTION " + (shown + 1) + " / " + form.Length, 34, UIFactory.Cyan, TextAnchor.MiddleCenter, 1200, 56);
        UIFactory.Label(col, q.prompt, 32, UIFactory.White, TextAnchor.MiddleCenter, 1150, 170);
        Button first = null;
        for (int d = 0; d < optOrder[qi].Length; d++)
        {
            int display = d;
            var b = UIFactory.MakeButton(col, (char)('A' + d) + ".  " + q.options[optOrder[qi][d]], () => Choose(qi, display), true, 1100, 72);
            b.GetComponentInChildren<Text>().fontSize = 24;
            if (first == null) first = b;
        }
        UIFactory.Label(col, "No feedback is shown during the test.", 20, UIFactory.Dim, TextAnchor.MiddleCenter, 1150, 34);
        UIFactory.MakeButton(col, "EXIT TEST (answers are discarded)", ShowIntro, true, 700, 52);
        UIFactory.Focus(first);
    }

    void Choose(int qi, int display)
    {
        chosen[qi] = optOrder[qi][display];
        shown++;
        if (shown >= form.Length) Submit();
        else ShowQuestion();
    }

    // ---------------- submit ----------------
    LocalQuiz ComputeLocal()
    {
        var lq = new LocalQuiz { total = form.Length, perThreat = new int[8] };
        for (int i = 0; i < form.Length; i++)
            if (chosen[i] == form[i].correct) { lq.score++; lq.perThreat[form[i].threat - 1]++; }
        return lq;
    }

    async void Submit()
    {
        bool remote = Online && pm.Profile.player.research_consent;
        if (!remote)
        {
            LocalQuizStore.Save(phase, ComputeLocal());
            ShowResults(phase, false);
            return;
        }

        var col = NewPanel();
        UIFactory.Label(col, "SUBMITTING YOUR ANSWERS...", 40, UIFactory.Amber, TextAnchor.MiddleCenter, 1200, 70);
        var res = await pm.SubmitQuiz(phase, testSet, chosen);   // answers are in question-id order; the server scores them
        if (this == null) return;

        if (res.Ok) { ShowResults(phase, true); return; }

        col = NewPanel();
        UIFactory.Label(col, "COULD NOT SAVE YOUR RESULTS", 44, UIFactory.Red, TextAnchor.MiddleCenter, 1200, 70);
        UIFactory.Label(col, res.Message + (res.Retryable ? "\nNothing was lost - you can retry (it will not be saved twice)." : ""), 26, UIFactory.White, TextAnchor.MiddleCenter, 1150, 130);
        Button first = null;
        if (res.Retryable) first = UIFactory.MakeButton(col, "RETRY SAVING", Submit);
        UIFactory.MakeButton(col, "KEEP RESULTS ON THIS DEVICE ONLY", () => { LocalQuizStore.Save(phase, ComputeLocal()); ShowResults(phase, false); });
        var back = UIFactory.MakeButton(col, "DISCARD AND GO BACK", ShowIntro);
        UIFactory.Focus(first != null ? first : back);
    }

    // ---------------- results ----------------
    void ShowResults(string justTaken, bool savedRemote)
    {
        if (justTaken != null) lastRemote = savedRemote;
        var col = NewPanel();
        var pre = GetResult("pre"); var post = GetResult("post");
        bool both = pre.has && post.has;

        string title = justTaken == "pre" ? "PRE-TEST COMPLETE" : justTaken == "post" ? "POST-TEST COMPLETE" : "YOUR RESULTS";
        UIFactory.Label(col, title, 54, UIFactory.Green, TextAnchor.MiddleCenter, 1200, 80);
        if (justTaken != null)
            UIFactory.Label(col, savedRemote ? "Saved to the project database (research consent given)." : "Saved on this device only.", 24, UIFactory.Gold, TextAnchor.MiddleCenter, 1150, 36);

        var sb = new StringBuilder();
        if (pre.has) sb.Append("PRE-TEST:  ").Append(pre.score).Append('/').Append(pre.total).Append("  (").Append(Pct(pre)).Append("%)\n");
        if (post.has) sb.Append("POST-TEST: ").Append(post.score).Append('/').Append(post.total).Append("  (").Append(Pct(post)).Append("%)\n");

        if (both)
        {
            sb.Append("\nBY TOPIC (pre -> post, out of 2)\n");
            for (int i = 0; i < 8; i++)
            {
                int d = post.threat[i] - pre.threat[i];
                sb.Append(QuestionBank.ThreatNames[i]).Append(":  ").Append(pre.threat[i]).Append(" -> ").Append(post.threat[i])
                  .Append("   ").Append(d > 0 ? "+" + d : d.ToString()).Append('\n');
            }
            int diff = post.score - pre.score;
            sb.Append("\nOVERALL CHANGE: ").Append(diff > 0 ? "+" + diff : diff.ToString()).Append(" questions");
            if (pre.total > pre.score) sb.Append("     NORMALIZED GAIN: ").Append(Mathf.RoundToInt(100f * diff / (pre.total - pre.score))).Append('%');
        }
        else if (pre.has && justTaken == "pre")
        {
            var weak = new List<string>();
            for (int i = 0; i < 8; i++) if (pre.threat[i] < 2) weak.Add(QuestionBank.ThreatNames[i]);
            sb.Append("\nTopics to focus on: ").Append(weak.Count > 0 ? string.Join(", ", weak) : "none - great start!").Append('\n');
            sb.Append("Visit the LEARNING CENTER and play the campaign.\n");
            sb.Append(showExplanationsAfterPre ? "" : "Answers and explanations unlock after the post-test (so the comparison stays fair).");
        }
        UIFactory.Label(col, sb.ToString().TrimEnd(), 25, UIFactory.White, TextAnchor.UpperLeft, 1000, 480);

        if (both)
            UIFactory.Label(col, "These scores show how you did on these questions. Whether the game helps people learn is judged across many players in the capstone study.",
                20, UIFactory.Dim, TextAnchor.MiddleCenter, 1150, 56);

        Button first = null;
        bool canReview = form != null && justTaken != null && (justTaken == "post" || showExplanationsAfterPre);
        if (canReview) first = UIFactory.MakeButton(col, "REVIEW ANSWERS & EXPLANATIONS", () => { reviewIndex = 0; ShowReview(); });
        var back = UIFactory.MakeButton(col, "BACK TO TEST MENU", ShowIntro);
        UIFactory.MakeButton(col, "MAIN MENU", GoBack);
        UIFactory.Focus(first != null ? first : back);
    }

    static int Pct(Result r) { return r.total > 0 ? Mathf.RoundToInt(100f * r.score / r.total) : 0; }

    // ---------------- review (post-test) ----------------
    void ShowReview()
    {
        var col = NewPanel();
        int qi = qOrder[reviewIndex];
        var q = form[qi];
        bool ok = chosen[qi] == q.correct;
        UIFactory.Label(col, "REVIEW   -   " + (reviewIndex + 1) + " / " + form.Length + "   (" + QuestionBank.ThreatNames[q.threat - 1] + ")", 32, UIFactory.Cyan, TextAnchor.MiddleCenter, 1200, 56);
        UIFactory.Label(col, q.prompt, 30, UIFactory.White, TextAnchor.MiddleCenter, 1150, 130);
        string mine = chosen[qi] >= 0 ? q.options[chosen[qi]] : "(no answer)";
        UIFactory.Label(col, "Your answer: " + mine, 26, ok ? UIFactory.Green : UIFactory.Red, TextAnchor.MiddleCenter, 1150, 80);
        if (!ok) UIFactory.Label(col, "Correct answer: " + q.options[q.correct], 26, UIFactory.Green, TextAnchor.MiddleCenter, 1150, 80);
        UIFactory.Label(col, q.explanation, 25, UIFactory.Amber, TextAnchor.MiddleCenter, 1150, 110);

        Button next = null;
        if (reviewIndex < form.Length - 1) next = UIFactory.MakeButton(col, "NEXT QUESTION", () => { reviewIndex++; ShowReview(); });
        if (reviewIndex > 0) UIFactory.MakeButton(col, "PREVIOUS QUESTION", () => { reviewIndex--; ShowReview(); });
        var done = UIFactory.MakeButton(col, "BACK TO RESULTS", () => ShowResults(phase, lastRemote));
        UIFactory.Focus(next != null ? next : done);
    }
}
