using Godot;
using SteelAndSalt.Interaction;

namespace SteelAndSalt.Journal;

/// <summary>
/// Подбираемая записка/предмет: при взаимодействии добавляет Entry в журнал (с меткой «!»)
/// и убирает объект из мира. Вешается дочерним Area3D на модель предмета, как Interactable.
/// </summary>
[GlobalClass]
public partial class JournalPickup : Interactable
{
    [Signal] public delegate void PickedUpEventHandler(JournalEntry entry);

    [ExportGroup("Journal")]
    [Export] public JournalEntry Entry { get; set; }
    /// <summary>Что удалить после подбора. Пусто — родитель (сам предмет), либо этот узел, если родитель — корень уровня.</summary>
    [Export] public Node RemoveOnPickup { get; set; }
    [Export] public bool KeepInWorld { get; set; }

    public JournalPickup()
    {
        Type = InteractionType.PickUp;
        SingleUse = true;
    }

    public override void _Ready()
    {
        // уже в журнале (например, после загрузки сохранения) — второй раз не подбирается
        if (!KeepInWorld && Entry is not null && JournalManager.Instance?.Has(Entry) == true)
            RemoveFromWorld();
    }

    protected override InteractionResult OnInteract(in InteractionContext context)
    {
        var journal = JournalManager.Instance;
        if (Entry is null || journal is null)
        {
            GD.PushError($"{Name}: не задан Entry или нет autoload JournalManager.");
            return InteractionResult.Failed;
        }

        journal.Add(Entry);
        EmitSignal(SignalName.PickedUp, Entry);

        if (!KeepInWorld)
            RemoveFromWorld();
        return InteractionResult.Success;
    }

    private void RemoveFromWorld()
    {
        var target = RemoveOnPickup;
        if (target is null)
        {
            var parent = GetParent();
            target = parent is null || parent == GetTree().CurrentScene ? this : parent;
        }
        target.QueueFree();
    }
}
