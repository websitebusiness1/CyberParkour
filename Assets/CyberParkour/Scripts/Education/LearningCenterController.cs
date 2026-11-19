using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Learning Center: one topic per threat, three short pages each:
///   1) what it is + a realistic example   2) warning signs + how to stay safe   3) knowledge check with feedback.
/// A wrong answer explains the concept and lets the player try again. Awareness and defence only - no attack how-tos.
/// </summary>
public class LearningCenterController : MonoBehaviour
{
    public string menuScene = "MainMenu";

    static readonly string[] PageNames = { "WHAT IT IS", "WARNING SIGNS & HOW TO STAY SAFE", "KNOWLEDGE CHECK" };

    Canvas canvas;
    RectTransform listRoot, contentRoot;
    Text headerText, progressText;
    Button prevBtn, nextBtn, backBtn;
    int topic, page;

    // knowledge check state
    int[] order;
    bool solved;
    Text feedback;
    readonly List<Button> optionButtons = new List<Button>();

    void Start()
    {
        Time.timeScale = 1f;
        UIFactory.EnsureEventSystem();
        PlayerProgressionManager.Ensure();
        canvas = UIFactory.CreateCanvas("LearningCanvas");
        Build();
        Select(0, 0);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) GoBack();
    }

    Text Anchored(string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var t = UIFactory.Label(canvas.transform, text, size, color, align);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    Button Corner(string label, UnityEngine.Events.UnityAction act, Vector2 anchor, Vector2 pos, float w)
    {
        var b = UIFactory.MakeButton(canvas.transform, label, act, true, w, 56);
        var le = b.GetComponent<LayoutElement>();
        if (le != null) Destroy(le);
        var rt = b.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, 56);
        return b;
    }

    void Build()
    {
        Anchored("LEARNING CENTER", 56, UIFactory.Cyan, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(1000, 76));
        progressText = Anchored("", 26, UIFactory.Gold, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(1000, 40));

        listRoot = UIFactory.MakeAnchoredColumn(canvas.transform, "Topics", new Vector2(0.02f, 0.14f), new Vector2(0.30f, 0.86f), 8f, TextAnchor.UpperLeft);
        contentRoot = UIFactory.MakeAnchoredColumn(canvas.transform, "Content", new Vector2(0.32f, 0.14f), new Vector2(0.98f, 0.86f), 12f, TextAnchor.UpperLeft, new Color(0f, 0.03f, 0.07f, 0.82f));

        prevBtn = Corner("< PREVIOUS", () => Select(topic, page - 1), new Vector2(0.45f, 0f), new Vector2(0, 40), 280);
        nextBtn = Corner("NEXT >", () => Select(topic, page + 1), new Vector2(0.62f, 0f), new Vector2(0, 40), 280);
        backBtn = Corner("BACK TO MENU", GoBack, new Vector2(0.86f, 0f), new Vector2(0, 40), 340);
    }

    // ---------------- topic list ----------------
    void RebuildList()
    {
        foreach (Transform t in listRoot) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
        int done = LearningProgressManager.CompletedCount();
        progressText.text = "TOPICS COMPLETED: " + done + " / 8";
        for (int i = 0; i < LessonBank.All.Length; i++)
        {
            var l = LessonBank.All[i];
            int idx = i;
            string mark = LearningProgressManager.IsCompleted(l.threat) ? "  [DONE]" : "";
            var b = UIFactory.MakeButton(listRoot, (i + 1) + ".  " + l.name.ToUpper() + mark, () => Select(idx, 0), true, 520, 52);
            var txt = b.GetComponentInChildren<Text>();
            txt.fontSize = 22;
            txt.color = i == topic ? UIFactory.Cyan : (LearningProgressManager.IsCompleted(l.threat) ? UIFactory.Green : UIFactory.White);
        }
    }

    // ---------------- pages ----------------
    void Select(int newTopic, int newPage)
    {
        topic = Mathf.Clamp(newTopic, 0, LessonBank.All.Length - 1);
        page = Mathf.Clamp(newPage, 0, 2);
        RebuildList();

        foreach (Transform t in contentRoot) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
        optionButtons.Clear();
        var lesson = LessonBank.All[topic];

        UIFactory.Label(contentRoot, lesson.name.ToUpper(), 46, UIFactory.Cyan, TextAnchor.MiddleLeft, 1000, 64);
        UIFactory.Label(contentRoot, "PAGE " + (page + 1) + " / 3  -  " + PageNames[page], 24, UIFactory.Gold, TextAnchor.MiddleLeft, 1000, 36);

        if (page == 0) PageWhatItIs(lesson);
        else if (page == 1) PageSigns(lesson);
        else PageCheck(lesson);

        prevBtn.interactable = !(topic == 0 && page == 0);
        nextBtn.interactable = !(topic == LessonBank.All.Length - 1 && page == 2);
        if (page == 2 && optionButtons.Count > 0) UIFactory.Focus(optionButtons[0]);
        else UIFactory.Focus(nextBtn.interactable ? nextBtn : backBtn);
    }

    void Body(string text, int size = 27, float h = -1f)
    {
        UIFactory.Label(contentRoot, text, size, UIFactory.White, TextAnchor.UpperLeft, 1000, h);
    }

    void PageWhatItIs(Lesson l)
    {
        Body("<color=#33E6FF>WHAT IS IT?</color>\n" + l.definition, 28, 150);
        Body("<color=#FFD633>A REAL-WORLD EXAMPLE</color>\n" + l.example, 28, 190);
    }

    void PageSigns(Lesson l)
    {
        var sb = new StringBuilder("<color=#FF6670>WARNING SIGNS</color>\n");
        foreach (var s in l.warningSigns) sb.Append("  -  ").Append(s).Append('\n');
        Body(sb.ToString().TrimEnd(), 26, 190);

        var sb2 = new StringBuilder("<color=#40FF8C>HOW TO STAY SAFE</color>\n");
        foreach (var s in l.defenses) sb2.Append("  -  ").Append(s).Append('\n');
        Body(sb2.ToString().TrimEnd(), 26, 210);
    }

    void PageCheck(Lesson l)
    {
        var q = l.check;
        solved = false;
        order = new int[q.options.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        for (int i = order.Length - 1; i > 0; i--)       // shuffle so the answer position isn't predictable
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        Body(q.prompt, 29, 110);
        for (int d = 0; d < order.Length; d++)
        {
            int display = d;
            var b = UIFactory.MakeButton(contentRoot, (char)('A' + d) + ".  " + q.options[order[d]], () => Pick(l, display), true, 960, 62);
            b.GetComponentInChildren<Text>().fontSize = 23;
            optionButtons.Add(b);
        }
        feedback = UIFactory.Label(contentRoot, LearningProgressManager.IsCompleted(l.threat)
            ? "You already completed this topic. Try the question again anytime."
            : "Choose the best answer.", 25, UIFactory.Amber, TextAnchor.UpperLeft, 1000, 110);
    }

    void Pick(Lesson l, int display)
    {
        if (solved) return;
        var q = l.check;
        int original = order[display];
        if (original == q.correct)
        {
            solved = true;
            feedback.color = UIFactory.Green;
            feedback.text = "CORRECT!  " + q.explanation;
            foreach (var b in optionButtons) b.interactable = false;
            LearningProgressManager.MarkCompleted(l.threat);
            AudioManager.Play(Sfx.Token);
            RebuildList();
            UIFactory.Focus(nextBtn.interactable ? nextBtn : backBtn);
        }
        else
        {
            feedback.color = UIFactory.Red;
            feedback.text = "NOT QUITE.  " + q.explanation + "\nTry another answer.";
            optionButtons[display].interactable = false;
            AudioManager.Play(Sfx.Hit);
            foreach (var b in optionButtons) if (b.interactable) { UIFactory.Focus(b); break; }
        }
    }

    void GoBack() { SceneManager.LoadScene(menuScene); }
}
