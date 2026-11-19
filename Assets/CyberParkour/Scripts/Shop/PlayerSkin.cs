using UnityEngine;

/// <summary>
/// Applies a character's COSMETIC look (body colour, visor colour, one accessory). Characters have identical
/// gameplay stats. Uses MaterialPropertyBlocks, so no runtime materials/shaders are created.
/// </summary>
public class PlayerSkin : MonoBehaviour
{
    public Renderer body;
    public Renderer visor;
    public Renderer[] extraAccents;     // chest core, belt, etc. (tinted with the accent colour)
    public GameObject[] accessories;   // children named "Acc_<accessory id>"
    public bool applySelectedOnStart = true;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;

    void Start()
    {
        if (!applySelectedOnStart) return;
        var pm = PlayerProgressionManager.Instance;
        var c = pm != null ? pm.SelectedCharacter : null;
        if (c != null) Apply(c);
    }

    public void Apply(CharacterDto c)
    {
        if (c == null) return;
        Tint(body, ParseColor(c.body_hex, new Color(0.1f, 0.5f, 0.9f)), 0.5f);
        Color accent = ParseColor(c.accent_hex, new Color(0.2f, 1f, 1f));
        Tint(visor, accent, 3f);
        if (extraAccents != null) foreach (var r in extraAccents) Tint(r, accent, 2.5f);
        if (accessories != null)
            foreach (var a in accessories)
                if (a != null) a.SetActive(a.name == "Acc_" + c.accessory);
    }

    void Tint(Renderer r, Color c, float emission)
    {
        if (r == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        mpb.SetColor(ColorId, c);
        mpb.SetColor(EmissionId, c * emission);
        r.SetPropertyBlock(mpb);
    }

    static Color ParseColor(string hex, Color fallback)
    {
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex.TrimStart('#'), out var c)) return c;
        return fallback;
    }
}
