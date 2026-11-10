using System;
using Godot;

namespace SteelAndSalt.Interaction;

/// <summary>Что показать в UI для текущей цели.</summary>
public readonly record struct InteractionPrompt(InteractionData Data, bool Available, string Reason);

/// <summary>
/// Луч игрока, который ищет IInteractable, управляет фокусом и обрабатывает Press/Hold.
/// Вешается дочерним узлом на Camera3D игрока (луч смотрит по -Z камеры).
/// </summary>
[GlobalClass]
public partial class Interactor : RayCast3D
{
    [Signal] public delegate void TargetChangedEventHandler(Node target);          // null — цели нет
    [Signal] public delegate void HoldProgressChangedEventHandler(float progress);  // 0..1, 0 — сброс
    [Signal] public delegate void InteractionPerformedEventHandler(Node target, int result);

    /// <summary>Подсказка изменилась (текст, доступность, причина). null — прятать UI.</summary>
    public event Action<InteractionPrompt?> PromptChanged;

    /// <summary>Кто взаимодействует (тело игрока). Пусто — берётся Owner сцены.</summary>
    [Export] public Node3D Actor { get; set; }
    [Export] public StringName InteractAction { get; set; } = "interact";
    [Export] public float Reach { get; set; } = 3f;

    public IInteractable CurrentTarget { get; private set; }
    public InteractionPrompt? CurrentPrompt { get; private set; }

    private float _holdTime;
    private bool _waitForRelease;   // после завершения Hold не запускать новый, пока кнопку не отпустят

    public override void _Ready()
    {
        Actor ??= Owner as Node3D ?? GetParentOrNull<Node3D>();
        TargetPosition = new Vector3(0, 0, -Reach);
        CollideWithAreas = true;
        CollideWithBodies = true;

        if (Actor is CollisionObject3D self)
            AddException(self);

        EnsureInputAction();
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateTarget(IsColliding() ? Resolve(GetCollider()) : null);

        if (CurrentTarget is null)
            return;

        var prompt = BuildPrompt(CurrentTarget);
        if (prompt != CurrentPrompt)
        {
            CurrentPrompt = prompt;
            PromptChanged?.Invoke(prompt);
        }

        if (Input.IsActionJustReleased(InteractAction))
            _waitForRelease = false;

        if (prompt.Data.RequiresHold)
            ProcessHold(prompt, (float)delta);
        else if (prompt.Available && Input.IsActionJustPressed(InteractAction))
            Perform();
    }

    private void ProcessHold(InteractionPrompt prompt, float delta)
    {
        bool holding = prompt.Available && !_waitForRelease && Input.IsActionPressed(InteractAction);
        if (!holding)
        {
            CancelHold();
            return;
        }

        _holdTime += delta;
        float progress = Mathf.Clamp(_holdTime / prompt.Data.HoldDuration, 0f, 1f);
        CurrentTarget.OnHoldProgress(Actor, progress);
        EmitSignal(SignalName.HoldProgressChanged, progress);

        if (progress >= 1f)
        {
            _holdTime = 0f;
            _waitForRelease = true;
            EmitSignal(SignalName.HoldProgressChanged, 0f);
            Perform();
        }
    }

    private void Perform()
    {
        var target = CurrentTarget;
        var result = target.Interact(new InteractionContext(Actor, Actor?.GlobalPosition ?? GlobalPosition));
        EmitSignal(SignalName.InteractionPerformed, target as Node, (int)result);
    }

    private void CancelHold()
    {
        if (_holdTime <= 0f)
            return;

        _holdTime = 0f;
        if (IsAlive(CurrentTarget))
            CurrentTarget.OnHoldCanceled(Actor);
        EmitSignal(SignalName.HoldProgressChanged, 0f);
    }

    private void UpdateTarget(IInteractable next)
    {
        if (!IsAlive(CurrentTarget))
            CurrentTarget = null;

        if (ReferenceEquals(next, CurrentTarget))
            return;

        CancelHold();
        CurrentTarget?.OnFocusExited(Actor);

        CurrentTarget = next;
        CurrentTarget?.OnFocusEntered(Actor);
        EmitSignal(SignalName.TargetChanged, next as Node);

        if (next is null && CurrentPrompt is not null)
        {
            CurrentPrompt = null;
            PromptChanged?.Invoke(null);
        }
    }

    private InteractionPrompt BuildPrompt(IInteractable target)
    {
        bool available = target.CanInteract(Actor, out var reason);
        return new InteractionPrompt(target.GetInteractionData(Actor), available, reason);
    }

    /// <summary>
    /// Коллайдер сам может быть IInteractable (Area3D-компонент),
    /// либо телом, у которого IInteractable среди детей — тогда берём с наибольшим Priority.
    /// </summary>
    private IInteractable Resolve(GodotObject collider)
    {
        if (collider is IInteractable direct)
            return direct;

        if (collider is not Node node)
            return null;

        IInteractable best = null;
        int bestPriority = int.MinValue;
        foreach (var child in node.GetChildren())
        {
            if (child is not IInteractable candidate)
                continue;

            int priority = candidate.GetInteractionData(Actor).Priority;
            if (priority > bestPriority)
            {
                best = candidate;
                bestPriority = priority;
            }
        }
        return best;
    }

    private static bool IsAlive(IInteractable target) =>
        target is not null && (target is not GodotObject obj || IsInstanceValid(obj));

    /// <summary>Если в Input Map нет действия — создаём с клавишей E, чтобы всё работало «из коробки».</summary>
    private void EnsureInputAction()
    {
        if (InputMap.HasAction(InteractAction))
            return;

        InputMap.AddAction(InteractAction);
        InputMap.ActionAddEvent(InteractAction, new InputEventKey { PhysicalKeycode = Key.E });
        GD.PushWarning($"Input action '{InteractAction}' не найдено — добавлено временно на клавишу E. Добавь его в Project Settings → Input Map.");
    }
}
