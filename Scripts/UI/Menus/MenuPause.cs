using Godot;

namespace SteelAndSalt.Menus;

public partial class MenuPause : Control
{
    private const string MainMenuPath = "res://Scenes/Prefabs/UI/Menus/MenuMain.tscn";
    private static readonly PackedScene SettingsScene =
        GD.Load<PackedScene>("res://Scenes/Prefabs/UI/Menus/SettingsMenu.tscn");

    private Control _buttons;
    private Button _resume;
    private MenuSettings _settings;
    private Input.MouseModeEnum _previousMouseMode;

    public override void _Ready()
    {
        _buttons = GetNode<Control>("Center/Buttons");
        _resume = _buttons.GetNode<Button>("Resume");
        _resume.Pressed += Resume;
        _buttons.GetNode<Button>("Settings").Pressed += OpenSettings;
        _buttons.GetNode<Button>("MainMenu").Pressed += ReturnToMainMenu;
        Hide();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!input.IsActionPressed("ui_cancel") || input.IsEcho())
            return;

        if (Visible)
        {
            if (_settings != null)
                _settings.CancelChanges();
            else
                Resume();
        }
        else
        {
            if (GetTree().Paused)
                return;

            _previousMouseMode = Input.MouseMode;
            GetTree().Paused = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            Show();
            _resume.GrabFocus();
        }

        GetViewport().SetInputAsHandled();
    }

    private void Resume()
    {
        Hide();
        GetTree().Paused = false;
        Input.MouseMode = _previousMouseMode;
    }

    private void OpenSettings()
    {
        _settings = SettingsScene.Instantiate<MenuSettings>();
        AddChild(_settings);
        _settings.VisibilityChanged += OnSettingsVisibilityChanged;
        _buttons.Hide();
    }

    private void OnSettingsVisibilityChanged()
    {
        if (_settings.Visible)
            return;

        _settings.VisibilityChanged -= OnSettingsVisibilityChanged;
        _settings.QueueFree();
        _settings = null;
        _buttons.Show();
        _resume.GrabFocus();
    }

    private void ReturnToMainMenu()
    {
        var tree = GetTree();
        var error = tree.ChangeSceneToFile(MainMenuPath);
        if (error == Error.Ok)
            tree.Paused = false;
        else
            GD.PushError($"Could not open the main menu: {error}");
    }
}
