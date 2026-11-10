using Godot;

namespace SteelAndSalt.Interaction;

/// <summary>
/// Базовая реализация IInteractable как компонент-Area3D.
/// Вешается дочерним узлом на дверь/сундук/NPC; наследники переопределяют OnInteract.
/// Для RayCast3D игрока включи collide_with_areas.
/// </summary>
[GlobalClass]
public partial class Interactable : Area3D, IInteractable
{
    [Signal] public delegate void InteractedEventHandler(Node interactor);
    [Signal] public delegate void FocusChangedEventHandler(bool focused);

    [ExportGroup("Interaction")]
    [Export] public InteractionType Type { get; set; } = InteractionType.Use;
    [Export] public InteractionTrigger Trigger { get; set; } = InteractionTrigger.Press;
    [Export(PropertyHint.Range, "0,10,0.05,suffix:s")] public float HoldDuration { get; set; } = 1f;
    [Export] public int Priority { get; set; }

    [ExportGroup("UI")]
    /// <summary>Пусто — используется текст по умолчанию для Type.</summary>
    [Export] public string PromptText { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }

    [ExportGroup("Rules")]
    [Export] public bool Enabled { get; set; } = true;
    [Export] public bool SingleUse { get; set; }
    [Export(PropertyHint.Range, "0,60,0.1,suffix:s")] public float Cooldown { get; set; }
    [Export] public float MaxDistance { get; set; } = 2.5f;

    public InteractionType InteractionType => Type;
    public bool IsUsed { get; private set; }

    private double _cooldownEndsAt;

    public virtual InteractionData GetInteractionData(Node interactor) => new(
        Type,
        Trigger,
        string.IsNullOrEmpty(PromptText) ? DefaultPrompt(Type) : Tr(PromptText),
        Icon,
        HoldDuration,
        Priority);

    public virtual bool CanInteract(Node interactor, out string reason)
    {
        reason = null;
        if (!Enabled) return false;
        if (SingleUse && IsUsed) return false;

        if (Now < _cooldownEndsAt)
        {
            reason = Tr("Недоступно");
            return false;
        }

        if (interactor is Node3D body && body.GlobalPosition.DistanceTo(GlobalPosition) > MaxDistance)
        {
            reason = Tr("Слишком далеко");
            return false;
        }
        return true;
    }

    public InteractionResult Interact(in InteractionContext context)
    {
        if (!CanInteract(context.Interactor, out _))
            return InteractionResult.Blocked;

        var result = OnInteract(context);
        if (result != InteractionResult.Success)
            return result;

        IsUsed = true;
        _cooldownEndsAt = Now + Cooldown;
        EmitSignal(SignalName.Interacted, context.Interactor);
        return result;
    }

    /// <summary>Логика конкретного объекта. По умолчанию — только сигнал Interacted.</summary>
    protected virtual InteractionResult OnInteract(in InteractionContext context) => InteractionResult.Success;

    public virtual void OnFocusEntered(Node interactor) => EmitSignal(SignalName.FocusChanged, true);
    public virtual void OnFocusExited(Node interactor) => EmitSignal(SignalName.FocusChanged, false);
    public virtual void OnHoldProgress(Node interactor, float progress) { }
    public virtual void OnHoldCanceled(Node interactor) { }

    private static double Now => Time.GetTicksMsec() / 1000.0;

    public static string DefaultPrompt(InteractionType type) => TranslationServer.Translate(type switch
    {
        InteractionType.PickUp  => "Подобрать",
        InteractionType.Open    => "Открыть",
        InteractionType.Talk    => "Говорить",
        InteractionType.Examine => "Осмотреть",
        InteractionType.Toggle  => "Переключить",
        InteractionType.Sit     => "Сесть",
        InteractionType.Loot    => "Обыскать",
        InteractionType.Craft   => "Создать",
        _                       => "Использовать",
    });
}
