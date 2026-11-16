using Godot;

namespace SteelAndSalt.Journal;

/// <summary>
/// Кнопка-закладка, торчащая из-под края страницы. Переключает раздел журнала
/// и возвращает к его оглавлению. Показывает «!», если в разделе есть непрочитанное.
/// Активная закладка выдвигается дальше остальных.
/// </summary>
[GlobalClass]
public partial class JournalBookmark : Button
{
    [Export] public JournalSection Section { get; set; }
    /// <summary>Значок «!» (обычно дочерний Label). Показывается, пока в разделе есть непрочитанное.</summary>
    [Export] public Control UnreadBadge { get; set; }

    [ExportGroup("Default Style")]
    /// <summary>Выключи, если стилизуешь закладку своей текстурой через Theme / overrides.</summary>
    [Export] public bool UseDefaultStyle { get; set; } = true;
    [Export] public Color RibbonColor { get; set; } = new(0.55f, 0.12f, 0.10f);
    [Export] public Color TextColor { get; set; } = new(0.96f, 0.91f, 0.80f);
    [Export(PropertyHint.Range, "0,40,1,suffix:px")] public float HoverOffset { get; set; } = 6f;
    [Export(PropertyHint.Range, "0,40,1,suffix:px")] public float ActiveOffset { get; set; } = 16f;

    public override void _Ready()
    {
        ToggleMode = true;   // нажатое состояние = активный раздел (выставляет JournalUI)
        if (UseDefaultStyle)
            ApplyDefaultStyle();
        SetUnread(false);
    }

    public void SetUnread(bool unread)
    {
        if (UnreadBadge is not null)
            UnreadBadge.Visible = unread;
    }

    private void ApplyDefaultStyle()
    {
        var normal = Ribbon(RibbonColor.Darkened(0.2f), 0f);
        var hover = Ribbon(RibbonColor, HoverOffset);
        var active = Ribbon(RibbonColor.Lightened(0.08f), ActiveOffset);

        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", active);
        AddThemeStyleboxOverride("hover_pressed", active);
        AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        foreach (var name in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
            AddThemeColorOverride(name, TextColor);
    }

    /// <summary>Лента: скруглена справа, левый край прячется под страницу; ExpandMarginRight «выдвигает» её.</summary>
    private static StyleBoxFlat Ribbon(Color color, float extend) => new()
    {
        BgColor = color,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomRight = 4,
        ExpandMarginRight = extend,
        ContentMarginLeft = 18,
        ContentMarginRight = 10,
        ContentMarginTop = 6,
        ContentMarginBottom = 6,
        ShadowColor = new Color(0, 0, 0, 0.35f),
        ShadowSize = 3,
        ShadowOffset = new Vector2(2, 2),
    };
}
