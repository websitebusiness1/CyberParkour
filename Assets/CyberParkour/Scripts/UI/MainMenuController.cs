using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Cyber Parkour main menu: account flow (login/register/guest) over a 3D showroom.</summary>
public class MainMenuController : MonoBehaviour
{
    public string firstLevelScene = "Level01_Malware";
    public string shopScene = "CharacterSelection";

    const float W = 800f;   // label width that fits inside the left menu card

    Canvas canvas;
    GameObject panel;
    PlayerProgressionManager pm;

    InputField emailField, passwordField;
    Text statusText;
    readonly List<Button> busyButtons = new List<Button>();
    string lastNotice = "";

    async void Start()
    {
        Time.timeScale = 1f;
        UIFactory.EnsureEventSystem();
        canvas = UIFactory.CreateCanvas("MenuCanvas");
        pm = PlayerProgressionManager.Ensure();

        if (!pm.IsConfigured) { ShowNotConfigured(); return; }
        if (pm.Auth.IsSignedIn && pm.HasProfile) { ShowMainMenu(); return; }   // coming back from a level/shop

        ShowConnecting("Restoring session...");
        var r = await pm.StartSession();
        if (this == null) return;

        if (r.Ok)
        {
            var flush = await pm.FlushPendingCompletion();
            if (this == null) return;
            if (flush != null) lastNotice = flush.Ok ? "Recovered an unsaved level reward from your last run." : "";
            ShowMainMenu();
        }
        else if (r.Code == "no_session") ShowLogin("");
        else if (r.Code == "network") ShowLogin("Can't reach the database. Retry, or play offline (no saving).");
        else if (pm.Auth.IsSignedIn) ShowMainMenu();
        else ShowLogin(r.Message);
    }

    // ---------------- panels ----------------
    RectTransform NewPanel()
    {
        if (panel != null) Destroy(panel);
        busyButtons.Clear();
        return UIFactory.MakeColumnPanel(canvas.transform, "MenuPanel", out panel, 10f, 0.62f, true);
    }

    void Title(RectTransform col)
    {
        UIFactory.Label(col, "CYBER PARKOUR", 84, UIFactory.Cyan, TextAnchor.MiddleCenter, W, 110);
        UIFactory.Label(col, "A 3D PARKOUR GAME FOR CYBERSECURITY AWARENESS", 22, UIFactory.White, TextAnchor.MiddleCenter, W, 34);
        UIFactory.Label(col, "", 20, UIFactory.White, TextAnchor.MiddleCenter, W, 12);
    }

    void ShowConnecting(string message)
    {
        var col = NewPanel();
        Title(col);
        UIFactory.Label(col, message, 32, UIFactory.Amber, TextAnchor.MiddleCenter, W, 60);
    }

    void ShowNotConfigured()
    {
        var col = NewPanel();
        Title(col);
        UIFactory.Label(col, "DATABASE NOT CONFIGURED", 34, UIFactory.Red, TextAnchor.MiddleCenter, W, 56);
        UIFactory.Label(col, pm.ConfigProblem + "\nSee docs/DATABASE_SETUP.md. You can still play offline - coins and unlocks are NOT saved.",
            24, UIFactory.White, TextAnchor.MiddleCenter, W, 170);
        var play = UIFactory.MakeButton(col, "PLAY OFFLINE (NO SAVING)", PlayOffline);
        UIFactory.MakeButton(col, "EXIT", Quit);
        UIFactory.Focus(play);
    }

    void ShowLogin(string notice)
    {
        var col = NewPanel();
        Title(col);
        emailField = UIFactory.MakeInputField(col, "email", false);
        passwordField = UIFactory.MakeInputField(col, "password (8+ characters)", true);
        statusText = UIFactory.Label(col, notice, 22, UIFactory.Amber, TextAnchor.MiddleCenter, W, 70);

        var signIn = UIFactory.MakeButton(col, "SIGN IN", () => DoAuth(0));
        var register = UIFactory.MakeButton(col, "REGISTER NEW ACCOUNT", () => DoAuth(1));
        var guest = UIFactory.MakeButton(col, "PLAY AS GUEST", () => DoAuth(2));
        var offline = UIFactory.MakeButton(col, "PLAY OFFLINE (NO SAVING)", PlayOffline);
        var exit = UIFactory.MakeButton(col, "EXIT", Quit);
        busyButtons.AddRange(new[] { signIn, register, guest, offline, exit });
        UIFactory.Label(col, "Guest progress is saved on this device only. Register to keep it safe.", 18, UIFactory.Dim, TextAnchor.MiddleCenter, W, 40);
    }

    void ShowMainMenu()
    {
        var col = NewPanel();
        Title(col);

        // keep the 3D showroom runner in sync with the saved character
        var skin = FindAnyObjectByType<PlayerSkin>();
        if (skin != null && pm.SelectedCharacter != null) skin.Apply(pm.SelectedCharacter);

        if (pm.HasProfile)
        {
            var p = pm.Profile.player;
            var sel = pm.SelectedCharacter;
            string who = pm.Auth.IsGuest ? "GUEST " : "";
            UIFactory.Label(col, who + p.display_name.ToUpper() + (sel != null ? "  |  " + sel.name.ToUpper() : ""), 24, UIFactory.White, TextAnchor.MiddleCenter, W, 36);
            UIFactory.Label(col, "BANKED COINS: " + p.total_coins, 40, UIFactory.Gold, TextAnchor.MiddleCenter, W, 56);
            if (!string.IsNullOrEmpty(lastNotice)) UIFactory.Label(col, lastNotice, 20, UIFactory.Green, TextAnchor.MiddleCenter, W, 34);
            string testNotice = TestNotice();
            if (testNotice != "") UIFactory.Label(col, testNotice, 21, UIFactory.Amber, TextAnchor.MiddleCenter, W, 34);

            var play = UIFactory.MakeButton(col, "PLAY", PlaySavedProgress);
            UIFactory.MakeButton(col, "CHARACTER SHOP", () => SceneManager.LoadScene(shopScene));
            UIFactory.MakeButton(col, "LEVEL SELECTION", () => SceneManager.LoadScene("LevelSelection"));
            UIFactory.MakeButton(col, "LEARNING CENTER", () => SceneManager.LoadScene("CybersecurityLearningCenter"));
            UIFactory.MakeButton(col, "KNOWLEDGE TEST (PRE / POST)", () => SceneManager.LoadScene("CybersecurityKnowledgeTest"));
            UIFactory.MakeButton(col, "SIGN OUT", DoSignOut);
            UIFactory.MakeButton(col, "EXIT", Quit);
            UIFactory.Focus(play);
        }
        else
        {
            UIFactory.Label(col, "Signed in, but your progress could not be loaded.", 26, UIFactory.Red, TextAnchor.MiddleCenter, W, 70);
            statusText = UIFactory.Label(col, "", 22, UIFactory.Amber, TextAnchor.MiddleCenter, W, 50);
            var retry = UIFactory.MakeButton(col, "RETRY LOADING PROGRESS", RetryLoad);
            var offline = UIFactory.MakeButton(col, "PLAY OFFLINE (NO SAVING)", PlayOffline);
            var signOut = UIFactory.MakeButton(col, "SIGN OUT", DoSignOut);
            busyButtons.AddRange(new[] { retry, offline, signOut });
            UIFactory.Focus(retry);
        }
    }

    /// <summary>Gentle, non-blocking reminders about the knowledge tests.</summary>
    string TestNotice()
    {
        if (!pm.HasProfile || pm.Profile.quiz == null) return "";
        bool l8 = false;
        if (pm.Profile.levels != null)
            foreach (var l in pm.Profile.levels) if (l.id == 8) l8 = l.completed;
        if (l8 && !pm.Profile.quiz.has_post) return "CAMPAIGN COMPLETE! Take the POST-TEST in the Knowledge Test.";
        if (!pm.Profile.quiz.has_pre) return "Tip: take the PRE-TEST before you start the campaign.";
        return "";
    }

    // ---------------- actions ----------------
    void SetBusy(bool busy) { foreach (var b in busyButtons) if (b != null) b.interactable = !busy; }

    async void DoAuth(int mode)
    {
        string email = emailField != null ? emailField.text : "";
        string pass = passwordField != null ? passwordField.text : "";
        SetBusy(true);
        statusText.color = UIFactory.Amber;
        statusText.text = mode == 2 ? "Creating guest session..." : "Contacting server...";

        AuthResult r = mode == 0 ? await pm.Auth.SignIn(email, pass)
                     : mode == 1 ? await pm.Auth.SignUp(email, pass)
                     : await pm.Auth.SignInAsGuest();
        if (this == null) return;
        if (passwordField != null) passwordField.text = "";   // never keep the password around

        if (!r.Success)
        {
            statusText.color = r.NeedsEmailConfirmation ? UIFactory.Green : UIFactory.Red;
            statusText.text = r.Message;
            SetBusy(false);
            return;
        }

        statusText.text = "Loading your progress...";
        var lp = await pm.LoadProfile();
        if (this == null) return;
        if (lp.Ok)
        {
            var flush = await pm.FlushPendingCompletion();
            if (this == null) return;
            lastNotice = flush != null && flush.Ok ? "Recovered an unsaved level reward from your last run." : "";
        }
        ShowMainMenu();
    }

    async void RetryLoad()
    {
        SetBusy(true);
        statusText.text = "Loading...";
        var lp = await pm.LoadProfile();
        if (this == null) return;
        if (lp.Ok) ShowMainMenu();
        else { statusText.color = UIFactory.Red; statusText.text = lp.Message; SetBusy(false); }
    }

    async void DoSignOut()
    {
        await pm.SignOut();
        if (this == null) return;
        lastNotice = "";
        ShowLogin("Signed out.");
    }

    void PlayOffline() { SceneManager.LoadScene(firstLevelScene); }

    /// <summary>Loads the furthest unlocked level whose scene exists in the build.</summary>
    void PlaySavedProgress()
    {
        string target = firstLevelScene;
        if (pm.HasProfile && pm.Profile.levels != null)
            foreach (var l in pm.Profile.levels)
                if (l.unlocked && Application.CanStreamedLevelBeLoaded(l.scene_name)) target = l.scene_name;
        SceneManager.LoadScene(target);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
