using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Level selection: 8 cards showing lock / completion / best time / objective status.
/// Data comes from the database profile when signed in; otherwise only Level 1 is available (offline).</summary>
public class LevelSelectionController : MonoBehaviour
{
    public string menuScene = "MainMenu";

    struct Info { public string scene, threat, title; public Color color; }

    static readonly Info[] Defaults =
    {
        new Info { scene = "Level01_Malware",    threat = "MALWARE",           title = "The Infected Processor",        color = new Color(0.1f, 0.8f, 1f) },
        new Info { scene = "Level02_Phishing",   threat = "PHISHING",          title = "The Deceptive Network",         color = new Color(0.5f, 0.45f, 1f) },
        new Info { scene = "Level03_Ransomware", threat = "RANSOMWARE",        title = "The Locked Storage Vault",      color = new Color(1f, 0.25f, 0.3f) },
        new Info { scene = "Level04_Spyware",    threat = "SPYWARE",           title = "The Surveillance Corridor",     color = new Color(0.2f, 1f, 0.6f) },
        new Info { scene = "Level05_Trojan",     threat = "TROJAN HORSE",      title = "The Disguised Application Hub", color = new Color(1f, 0.6f, 0.2f) },
        new Info { scene = "Level06_DDoS",       threat = "DDOS ATTACK",       title = "The Overloaded Network",        color = new Color(1f, 0.8f, 0.2f) },
        new Info { scene = "Level07_MITM",       threat = "MAN-IN-THE-MIDDLE", title = "The Intercepted Connection",    color = new Color(0.9f, 0.3f, 0.9f) },
        new Info { scene = "Level08_Rootkit",    threat = "ROOTKIT",           title = "The Corrupted System Core",     color = new Color(0.95f, 0.95f, 0.8f) },
    };

    PlayerProgressionManager pm;
    Canvas canvas;
    Button backBtn;
    Text messageText;

    void Start()
    {
        Time.timeScale = 1f;
        UIFactory.EnsureEventSystem();
        pm = PlayerProgressionManager.Ensure();
        canvas = UIFactory.CreateCanvas("LevelSelectCanvas");
        Build();
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

    void Build()
    {
        Anchored("LEVEL SELECTION", 60, UIFactory.Cyan, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1000, 80));
        bool online = pm.HasProfile;
        Anchored(online ? "BANKED COINS: " + pm.Profile.player.total_coins : "OFFLINE - only Level 1 is available. Sign in from the main menu to unlock progress.",
            online ? 34 : 24, online ? UIFactory.Gold : UIFactory.Amber, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1400, 50));
        messageText = Anchored("", 26, UIFactory.Amber, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1400, 50));

        var grid = UIFactory.NewUI("Grid", canvas.transform);
        var gr = grid.GetComponent<RectTransform>();
        gr.anchorMin = new Vector2(0.04f, 0.2f);
        gr.anchorMax = new Vector2(0.96f, 0.84f);
        gr.offsetMin = gr.offsetMax = Vector2.zero;
        var glg = grid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(420f, 240f);
        glg.spacing = new Vector2(22f, 22f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;
        glg.childAlignment = TextAnchor.MiddleCenter;

        Button first = null;
        for (int i = 0; i < 8; i++)
        {
            var info = Defaults[i];
            bool unlocked = i == 0, completed = false, objective = false;
            float best = 0f; int tokens = 0;
            string scene = info.scene, threat = info.threat, title = info.title;

            if (online && pm.Profile.levels != null)
                foreach (var l in pm.Profile.levels)
                    if (l.id == i + 1)
                    {
                        unlocked = l.unlocked; completed = l.completed; objective = l.objective_complete;
                        best = l.best_time; tokens = l.best_tokens;
                        if (!string.IsNullOrEmpty(l.scene_name)) scene = l.scene_name;
                        if (!string.IsNullOrEmpty(l.threat_name)) threat = l.threat_name.ToUpper();
                        if (!string.IsNullOrEmpty(l.title)) title = l.title;
                    }

            bool exists = Application.CanStreamedLevelBeLoaded(scene);
            string status = !unlocked ? "LOCKED"
                          : !exists ? "COMING SOON"
                          : completed ? "COMPLETED   BEST " + UIFactory.FormatTime(best) + "\nTOKENS " + tokens + "   OBJECTIVE " + (objective ? "DONE" : "-")
                          : "UNLOCKED";

            string label = "LEVEL " + (i + 1) + "\n" + threat + "\n" + title + "\n" + status;
            string sceneCopy = scene;
            var btn = UIFactory.MakeButton(grid.transform, label, () => Play(sceneCopy), unlocked && exists, 420, 240);
            var txt = btn.GetComponentInChildren<Text>();
            txt.fontSize = 22;
            txt.color = unlocked ? Color.white : UIFactory.Dim;

            var cb = btn.colors;
            cb.normalColor = unlocked ? info.color * 0.35f + new Color(0, 0, 0, 0.65f) : new Color(0.08f, 0.1f, 0.14f, 0.85f);
            cb.highlightedColor = info.color * 0.8f + new Color(0, 0, 0, 0.2f);
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;
            if (first == null && unlocked && exists) first = btn;
        }

        backBtn = UIFactory.MakeButton(canvas.transform, "BACK TO MENU", GoBack, true, 420, 58);
        var br = backBtn.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0, 36);
        var le = backBtn.GetComponent<LayoutElement>();
        if (le != null) Destroy(le);
        br.sizeDelta = new Vector2(420, 58);

        UIFactory.Focus(first != null ? first : backBtn);
    }

    void Play(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene)) { messageText.text = "That level isn't built yet."; return; }
        SceneManager.LoadScene(scene);
    }

    void GoBack() { SceneManager.LoadScene(menuScene); }
}
