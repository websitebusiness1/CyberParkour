using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Character shop UI. Shows ONLY server-confirmed data. A purchase is reported as successful only after the
/// server accepted it; failures never change what is displayed.
/// </summary>
public class CharacterShopController : MonoBehaviour
{
    public PlayerSkin previewSkin;
    public string menuScene = "MainMenu";

    PlayerProgressionManager pm;
    Canvas canvas;
    Text balanceText, nameText, descText, statusText, messageText, confirmLabel;
    RectTransform listRoot;
    Button actionBtn, backBtn, confirmNo;
    Text actionLabel;
    GameObject confirmPanel;
    Action confirmYes;
    int selectedIndex;
    bool busy;

    void Start()
    {
        Time.timeScale = 1f;
        UIFactory.EnsureEventSystem();
        pm = PlayerProgressionManager.Ensure();
        canvas = UIFactory.CreateCanvas("ShopCanvas");
        Build();

        if (!pm.HasProfile)
        {
            ShowMessage("Sign in from the main menu to use the shop.\nCoins and unlocks are stored in the database.");
            actionBtn.interactable = false;
            actionLabel.text = "SIGN IN REQUIRED";
            UIFactory.Focus(backBtn);
            return;
        }
        for (int i = 0; i < pm.Profile.characters.Length; i++)
            if (pm.Profile.characters[i].selected) selectedIndex = i;
        Refresh();
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (confirmPanel.activeSelf) CancelConfirm();
            else if (!busy) GoBack();
        }
    }

    // ---------------- layout ----------------
    Text Anchored(string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var t = UIFactory.Label(canvas.transform, text, size, color, align);
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = pivot; rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        return t;
    }

    void Build()
    {
        Anchored("CHARACTER SHOP", 60, UIFactory.Cyan, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1000, 80));
        balanceText = Anchored("BANKED COINS: -", 36, UIFactory.Gold, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(1000, 50));
        messageText = Anchored("", 28, UIFactory.Amber, TextAnchor.LowerCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1400, 120));

        // left column: character list
        listRoot = UIFactory.MakeAnchoredColumn(canvas.transform, "List", new Vector2(0.02f, 0.14f), new Vector2(0.33f, 0.84f), 8f, TextAnchor.UpperLeft);
        // right column: info
        var info = UIFactory.MakeAnchoredColumn(canvas.transform, "Info", new Vector2(0.67f, 0.14f), new Vector2(0.98f, 0.84f), 12f, TextAnchor.UpperCenter, new Color(0f, 0.03f, 0.07f, 0.8f));
        nameText = UIFactory.Label(info, "", 38, UIFactory.Cyan, TextAnchor.MiddleCenter, 520, 100);
        descText = UIFactory.Label(info, "", 24, UIFactory.White, TextAnchor.UpperCenter, 520, 140);
        statusText = UIFactory.Label(info, "", 28, UIFactory.Gold, TextAnchor.MiddleCenter, 520, 60);
        actionBtn = UIFactory.MakeButton(info, "-", OnAction, true, 500, 60);
        actionLabel = actionBtn.GetComponentInChildren<Text>();
        backBtn = UIFactory.MakeButton(info, "BACK TO MENU", GoBack, true, 500, 56);

        // confirmation dialog
        var col = UIFactory.MakeColumnPanel(canvas.transform, "ConfirmPanel", out confirmPanel, 20f);
        confirmLabel = UIFactory.Label(col, "", 38, UIFactory.White, TextAnchor.MiddleCenter, 1000, 180);
        confirmNo = UIFactory.MakeButton(col, "NO, CANCEL", CancelConfirm);
        UIFactory.MakeButton(col, "YES, BUY", () => { var y = confirmYes; confirmYes = null; confirmPanel.SetActive(false); y?.Invoke(); });
        confirmPanel.SetActive(false);
    }

    // ---------------- refresh ----------------
    void Refresh()
    {
        var chars = pm.Profile.characters;
        balanceText.text = "BANKED COINS: " + pm.Profile.player.total_coins;
        selectedIndex = Mathf.Clamp(selectedIndex, 0, chars.Length - 1);

        foreach (Transform t in listRoot) { t.gameObject.SetActive(false); Destroy(t.gameObject); }
        Button focus = null;
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            string state = c.selected ? "SELECTED" : c.unlocked ? "UNLOCKED" : c.unlock_cost + " COINS";
            int idx = i;
            var b = UIFactory.MakeButton(listRoot, c.name.ToUpper() + "  -  " + state, () => { if (!busy) { selectedIndex = idx; Refresh(); } }, true, 540, 54);
            if (i == selectedIndex) focus = b;
        }
        UpdateInfo();
        if (focus != null && !busy) UIFactory.Focus(focus);
    }

    void UpdateInfo()
    {
        var c = pm.Profile.characters[selectedIndex];
        if (previewSkin != null) previewSkin.Apply(c);
        nameText.text = c.name.ToUpper();
        descText.text = c.description;

        if (!c.is_available) { statusText.text = "NOT AVAILABLE"; SetAction("UNAVAILABLE", false); }
        else if (c.selected) { statusText.text = "CURRENTLY SELECTED"; SetAction("SELECTED", false); }
        else if (c.unlocked) { statusText.text = "UNLOCKED"; SetAction("SELECT", true); }
        else { statusText.text = "LOCKED  -  " + c.unlock_cost + " COINS"; SetAction("BUY FOR " + c.unlock_cost + " COINS", true); }
    }

    void SetAction(string label, bool interactable) { actionLabel.text = label; actionBtn.interactable = interactable; }

    void ShowMessage(string m) { messageText.text = m; }

    // ---------------- actions ----------------
    void OnAction()
    {
        if (busy || !pm.HasProfile) return;
        var c = pm.Profile.characters[selectedIndex];

        if (c.unlocked && !c.selected) { DoSelect(c); return; }
        if (c.unlocked || !c.is_available) return;

        int balance = pm.Profile.player.total_coins;
        if (balance < c.unlock_cost)
        {
            ShowMessage("Not enough coins. You need " + (c.unlock_cost - balance) + " more.\nNothing was deducted.");
            return;
        }
        confirmLabel.text = "Buy " + c.name + " for " + c.unlock_cost + " coins?\nBalance after: " + (balance - c.unlock_cost);
        confirmYes = () => DoPurchase(c);
        confirmPanel.SetActive(true);
        UIFactory.Focus(confirmNo);
    }

    void CancelConfirm()
    {
        confirmYes = null;
        confirmPanel.SetActive(false);
        Refresh();
    }

    async void DoPurchase(CharacterDto c)
    {
        busy = true;
        actionBtn.interactable = false;
        ShowMessage("Processing purchase...");
        var r = await pm.PurchaseCharacter(c.id);
        busy = false;
        if (this == null) return;

        if (r.Ok) ShowMessage(r.Response != null && r.Response.duplicate
            ? "That purchase was already recorded. Nothing was charged twice."
            : "PURCHASED: " + c.name + " is now unlocked! (" + c.unlock_cost + " coins deducted)");
        else ShowMessage("Purchase failed: " + r.Message);
        Refresh();
    }

    async void DoSelect(CharacterDto c)
    {
        busy = true;
        actionBtn.interactable = false;
        ShowMessage("Saving selection...");
        var r = await pm.SelectCharacter(c.id);
        busy = false;
        if (this == null) return;

        ShowMessage(r.Ok ? c.name + " selected. It will be used in your next run." : "Could not select: " + r.Message);
        Refresh();
    }

    void GoBack()
    {
        if (busy) return;
        SceneManager.LoadScene(menuScene);
    }
}
