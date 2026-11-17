using Godot;

namespace SteelAndSalt.Menus;

/// <summary>
/// Главное меню. Кнопки открывают оверлеи (выбор сохранения, настройки, титры, подтверждение выхода)
/// поверх меню — инстансы префабов добавляются в OverlayContainer.
/// </summary>
public partial class MenuMain : Control
{
    [ExportGroup("Buttons")]
    [Export] private Button PlayButton { get; set; }
    [Export] private Button SettingsButton { get; set; }
    [Export] private Button CreditsButton { get; set; }
    [Export] private Button QuitButton { get; set; }

    [ExportGroup("Overlay")]
    [ExportSubgroup("Container")]
    /// <summary>Куда добавляются открытые оверлеи.</summary>
    [Export] private Control OverlayContainer { get; set; }

    [ExportSubgroup("Prefabs")]
    [Export] private PackedScene SaveSelectionScene { get; set; }
    [Export] private PackedScene SettingsScene { get; set; }
    [Export] private PackedScene CreditsScene { get; set; }
    /// <summary>Подтверждение выхода. Пусто — игра закрывается сразу.</summary>
    [Export] private PackedScene ConfirmationScene { get; set; }

    [ExportSubgroup("Game Entry")]
    /// <summary>Уровень, который загружается после выбора сохранения.</summary>
    [Export] private PackedScene GameLevelScene { get; set; }

    private string _saveFilePath;

    public override void _Ready()
    {
        if (PlayButton is not null) PlayButton.Pressed += () => OverlayOpen(SaveSelectionScene);
        if (SettingsButton is not null) SettingsButton.Pressed += () => OverlayOpen(SettingsScene);
        if (CreditsButton is not null) CreditsButton.Pressed += () => OverlayOpen(CreditsScene);
        if (QuitButton is not null) QuitButton.Pressed += OnQuitPressed;
    }

    // --- Оверлеи ---

    private Control OverlayOpen(PackedScene scene)
    {
        if (scene is null || OverlayContainer is null)
        {
            GD.PushError("MenuMain: оверлей или OverlayContainer не назначены в инспекторе.");
            return null;
        }

        var overlay = scene.Instantiate<Control>();
        OverlayContainer.AddChild(overlay);
        return overlay;
    }

    private void OverlayClose(Control overlay)
    {
        if (overlay is null || overlay.GetParent() != OverlayContainer)
            return;

        OverlayContainer.RemoveChild(overlay);
        overlay.QueueFree();
    }

    private void OnQuitPressed()
    {
        if (ConfirmationScene is null)
            GetTree().Quit();
        else
            OverlayOpen(ConfirmationScene);
    }
}
