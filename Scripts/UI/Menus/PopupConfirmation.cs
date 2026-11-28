using Godot;
using System;

namespace SteelAndSalt.Menus;

public partial class PopupConfirmation : ConfirmationDialog
{
    private Action onConfirm;
    private Action onCancel;

    public override void _Ready()
    {
        Confirmed += HandleConfirm;
        Canceled += HandleCancel;
    }

    private void HandleConfirm()
    {
        var callback = onConfirm;
        ClearCallbacks();
        callback?.Invoke();
    }

    private void HandleCancel()
    {
        var callback = onCancel;
        ClearCallbacks();
        callback?.Invoke();
    }

    /// damn this is so good  frfr
    public void Open(string text,
        Action onConfirmed, Action onCancelled,
        string confirmText, string cancelText)
    {
        DialogText = text;
        OkButtonText = confirmText;
        CancelButtonText = cancelText;
        onCancel = onCancelled;
        onConfirm = onConfirmed;

        ResetSize();
        PopupCentered();
        GetCancelButton().GrabFocus();
    }

    // this is even better on god
    public void Open(Action onConfirmed, Action onCancelled)
    {
        DialogText = "CONFIRMATION_DEFAULT_TEXT";
        OkButtonText = "CONFIRMATION_DEFAULT_CONFIRM";
        CancelButtonText = "CONFIRMATION_DEFAULT_CANCEL";
        onCancel = onCancelled;
        onConfirm = onConfirmed;

        ResetSize();
        PopupCentered();
        GetCancelButton().GrabFocus();
    }

    /// callbacks are crazy
    private void ClearCallbacks(){
        onCancel = null;
        onConfirm = null;
    }
}
