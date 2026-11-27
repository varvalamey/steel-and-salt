using Godot;
using System;

namespace SteelAndSalt.Menus;

public partial class MenuMain : Control
{
    [ExportGroup("Buttons")]
    [Export] private Button btnPlay;
    [Export] private Button btnSettings;
    [Export] private Button btnCredits;
    [Export] private Button btnQuit;

    [ExportGroup("Overlay")]
    [ExportSubgroup("Container")]
    [Export] private Control OverlayContainer;

    [ExportSubgroup("Prefabs")]
    [Export] private PackedScene scnSaveSelection;
    [Export] private PackedScene scnSettings;
    [Export] private PackedScene scnCredits;
    [Export] private PopupConfirmation popup;

    [ExportSubgroup("Game Entry")]
    [Export] private PackedScene scnGameLevel;

    private string SAVE_FILE_PATH;

    public override void _Ready()
    {
        btnPlay.Pressed += () => OverlayOpen(scnSaveSelection);
        btnSettings.Pressed += () => OverlayOpen(scnSettings);
        btnCredits.Pressed += () => OverlayOpen(scnCredits);
        btnQuit.Pressed += () => popup.Open(OnQuitPressed, null);
    }

    private void OverlayOpen(PackedScene scn)
    {
        if (scn != null)
        {
            Control overlayInstance = scn.Instantiate<Control>();
            OverlayContainer.AddChild(overlayInstance);
        }
        else GD.PrintErr("Overlay is not assigned in inspector");
    }

    public void OverlayClose(Control overlay)
    {
        if (overlay != null && overlay.GetParent() == OverlayContainer)
        {
            OverlayContainer.RemoveChild(overlay);
            overlay.QueueFree();
        }
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

}
