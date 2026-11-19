using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Builds UI from code so scenes need no hand-made canvases. Uses legacy UI Text
/// (no TextMeshPro essentials import needed); swap to TMP in the polish phase if desired.</summary>
public static class UIFactory
{
    public static readonly Color Cyan = new Color(0.2f, 0.9f, 1f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.2f);
    public static readonly Color Amber = new Color(1f, 0.7f, 0.15f);
    public static readonly Color Green = new Color(0.25f, 1f, 0.55f);
    public static readonly Color Red = new Color(1f, 0.3f, 0.35f);
    public static readonly Color White = new Color(0.95f, 0.97f, 1f);
    public static readonly Color Dim = new Color(0.55f, 0.6f, 0.7f);

    static Font font;
    public static Font DefaultFont
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas CreateCanvas(string name, int sortOrder = 0)
    {
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        var module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    public static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    public static Text Label(Transform parent, string text, int size, Color color,
        TextAnchor anchor = TextAnchor.MiddleCenter, float prefWidth = -1f, float prefHeight = -1f)
    {
        var go = NewUI("Label", parent);
        var t = go.AddComponent<Text>();
        t.font = DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        if (prefWidth > 0f || prefHeight > 0f)
        {
            var le = go.AddComponent<LayoutElement>();
            if (prefWidth > 0f) le.preferredWidth = prefWidth;
            if (prefHeight > 0f) le.preferredHeight = prefHeight;
        }
        return t;
    }

    public static Button MakeButton(Transform parent, string label, UnityAction onClick,
        bool interactable = true, float width = 520f, float height = 58f)
    {
        var go = NewUI("Button_" + label, parent);
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = new Color(0.05f, 0.18f, 0.28f, 0.95f);
        cb.highlightedColor = new Color(0.0f, 0.55f, 0.75f, 1f);
        cb.selectedColor = new Color(0.0f, 0.55f, 0.75f, 1f);
        cb.pressedColor = new Color(0.0f, 0.8f, 1f, 1f);
        cb.disabledColor = new Color(0.1f, 0.12f, 0.16f, 0.7f);
        cb.fadeDuration = 0.05f;
        btn.colors = cb;
        btn.interactable = interactable;
        if (onClick != null) btn.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;

        var t = Label(go.transform, label, 26, interactable ? White : Dim);
        Stretch(t.rectTransform);
        return btn;
    }

    public static void Focus(Selectable s)
    {
        if (EventSystem.current == null || s == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(s.gameObject);
    }

    /// <summary>
    /// Panel containing a vertical column. Default: full-screen dim backdrop with a centered column.
    /// leftSide=true: translucent card on the left 46% of the screen so a 3D scene stays visible on the right.
    /// </summary>
    public static RectTransform MakeColumnPanel(Transform parent, string name, out GameObject root, float spacing = 12f,
        float bgAlpha = 0.9f, bool leftSide = false)
    {
        root = NewUI(name, parent);
        Stretch(root.GetComponent<RectTransform>());
        if (!leftSide)
        {
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0.02f, 0.05f, bgAlpha);
        }

        var col = NewUI("Column", root.transform);
        var rt = col.GetComponent<RectTransform>();
        if (leftSide)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0.46f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = col.AddComponent<Image>();
            img.color = new Color(0f, 0.02f, 0.05f, bgAlpha);
            var accent = NewUI("AccentLine", col.transform);
            var ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(1f, 0f);
            ar.anchorMax = new Vector2(1f, 1f);
            ar.pivot = new Vector2(1f, 0.5f);
            ar.sizeDelta = new Vector2(4f, 0f);
            accent.AddComponent<Image>().color = Cyan;
            accent.AddComponent<LayoutElement>().ignoreLayout = true;
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1300f, 1000f);
        }
        var vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = spacing;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        return rt;
    }

    /// <summary>Translucent HUD plate with a coloured accent bar on its left edge.</summary>
    public static RectTransform Plate(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color accent, float alpha = 0.55f)
    {
        var go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0.03f, 0.07f, alpha);
        img.raycastTarget = false;

        var bar = NewUI("Accent", go.transform);
        var br = bar.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0f, 0f);
        br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 0.5f);
        br.sizeDelta = new Vector2(5f, 0f);
        var bi = bar.AddComponent<Image>();
        bi.color = accent;
        bi.raycastTarget = false;
        return rt;
    }

    public static string FormatTime(float t)
    {
        int m = (int)(t / 60f);
        float s = t - m * 60f;
        return m + ":" + s.ToString("00.0");
    }

    /// <summary>Anchored vertical column (optionally with a background tint). Returns the object that holds the children.</summary>
    public static RectTransform MakeAnchoredColumn(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        float spacing, TextAnchor align, Color? background = null)
    {
        var go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (background.HasValue)
        {
            var img = go.AddComponent<Image>();
            img.color = background.Value;
            img.raycastTarget = false;
        }
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = align;
        vlg.spacing = spacing;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;
        return rt;
    }

    public static InputField MakeInputField(Transform parent, string placeholder, bool password, float width = 520f, float height = 56f)
    {
        var go = NewUI("Input_" + placeholder, parent);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.04f, 0.1f, 0.16f, 0.95f);
        var input = go.AddComponent<InputField>();
        input.targetGraphic = img;

        var text = Label(go.transform, "", 26, White, TextAnchor.MiddleLeft);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(14f, 4f);
        text.rectTransform.offsetMax = new Vector2(-14f, -4f);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.supportRichText = false;

        var ph = Label(go.transform, placeholder, 26, Dim, TextAnchor.MiddleLeft);
        Stretch(ph.rectTransform);
        ph.rectTransform.offsetMin = new Vector2(14f, 4f);
        ph.rectTransform.offsetMax = new Vector2(-14f, -4f);
        ph.fontStyle = FontStyle.Italic;

        input.textComponent = text;
        input.placeholder = ph;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 120;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.preferredHeight = height;
        return input;
    }
}
