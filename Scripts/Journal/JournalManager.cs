using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SteelAndSalt.Journal;

/// <summary>
/// Состояние журнала: какие записи получены и какие ещё не прочитаны.
/// Регистрируется как autoload «Journal» (Project Settings → Globals → Autoload).
/// UI только читает отсюда данные и вызывает MarkRead.
/// </summary>
public partial class JournalManager : Node
{
    [Signal] public delegate void EntryAddedEventHandler(JournalEntry entry);
    [Signal] public delegate void EntryReadEventHandler(JournalEntry entry);
    /// <summary>Изменился набор непрочитанных (для «!» на закладках и в HUD).</summary>
    [Signal] public delegate void UnreadChangedEventHandler();
    /// <summary>Состояние полностью заменено (загрузка сохранения / очистка).</summary>
    [Signal] public delegate void ReloadedEventHandler();

    public static JournalManager Instance { get; private set; }

    private readonly List<JournalEntry> _entries = new();              // в порядке получения
    private readonly Dictionary<string, JournalEntry> _byKey = new();
    private readonly HashSet<string> _unread = new();

    public bool HasAnyUnread => _unread.Count > 0;

    public override void _EnterTree()
    {
        if (Instance is not null && Instance != this)
        {
            GD.PushWarning("JournalManager уже существует — лишний экземпляр удалён.");
            QueueFree();
            return;
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Добавить запись. Новая запись помечается непрочитанной. false — уже есть.</summary>
    public bool Add(JournalEntry entry)
    {
        if (entry is null)
            return false;

        var key = entry.Key;
        if (string.IsNullOrEmpty(key))
        {
            GD.PushError($"JournalEntry '{entry.Title}' без Id и без пути ресурса — сохрани его в .tres или задай Id.");
            return false;
        }
        if (_byKey.ContainsKey(key))
            return false;

        _entries.Add(entry);
        _byKey[key] = entry;
        _unread.Add(key);

        EmitSignal(SignalName.EntryAdded, entry);
        EmitSignal(SignalName.UnreadChanged);
        return true;
    }

    public bool Has(JournalEntry entry) => entry is not null && _byKey.ContainsKey(entry.Key);
    public bool Has(string key) => _byKey.ContainsKey(key);

    public bool IsUnread(JournalEntry entry) => entry is not null && _unread.Contains(entry.Key);

    public bool HasUnread(JournalSection section) =>
        _entries.Any(e => e.Section == section && _unread.Contains(e.Key));

    /// <summary>Снять «!» — вызывается, когда игрок впервые открыл страницу записи.</summary>
    public void MarkRead(JournalEntry entry)
    {
        if (entry is null || !_unread.Remove(entry.Key))
            return;

        EmitSignal(SignalName.EntryRead, entry);
        EmitSignal(SignalName.UnreadChanged);
    }

    /// <summary>Полученные записи раздела в порядке оглавления.</summary>
    public List<JournalEntry> GetEntries(JournalSection section) =>
        _entries.Where(e => e.Section == section)
                .OrderBy(e => e.SortOrder)   // OrderBy стабилен — внутри одного SortOrder порядок получения
                .ToList();

    public void Clear()
    {
        _entries.Clear();
        _byKey.Clear();
        _unread.Clear();
        EmitSignal(SignalName.Reloaded);
        EmitSignal(SignalName.UnreadChanged);
    }

    // --- Сохранение ---

    public Godot.Collections.Dictionary Save() => new()
    {
        ["entries"] = _entries.Select(e => e.ResourcePath).ToArray(),
        ["unread"] = _entries.Where(e => _unread.Contains(e.Key)).Select(e => e.ResourcePath).ToArray(),
    };

    public void Load(Godot.Collections.Dictionary data)
    {
        _entries.Clear();
        _byKey.Clear();
        _unread.Clear();

        var unreadPaths = data.TryGetValue("unread", out var u) ? u.AsStringArray().ToHashSet() : new HashSet<string>();
        var paths = data.TryGetValue("entries", out var e) ? e.AsStringArray() : [];

        foreach (var path in paths)
        {
            var entry = ResourceLoader.Exists(path) ? ResourceLoader.Load<JournalEntry>(path) : null;
            if (entry is null || _byKey.ContainsKey(entry.Key))
            {
                GD.PushWarning($"Journal: запись '{path}' не найдена при загрузке.");
                continue;
            }

            _entries.Add(entry);
            _byKey[entry.Key] = entry;
            if (unreadPaths.Contains(path))
                _unread.Add(entry.Key);
        }

        EmitSignal(SignalName.Reloaded);
        EmitSignal(SignalName.UnreadChanged);
    }
}
