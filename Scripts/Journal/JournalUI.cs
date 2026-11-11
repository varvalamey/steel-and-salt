using System.Collections.Generic;
using Godot;

namespace SteelAndSalt.Journal;


// Окно журнала. Два раздела (Записки / Предметы), переключаются закладками.
// В каждом разделе первая страница — оглавление (только полученные записи),
// по ссылке открывается страница записи. Непрочитанные помечены «!», пока страницу не открыли.

[GlobalClass]
public partial class JournalUI : Control
{
    [Signal] public delegate void OpenedEventHandler();
    [Signal] public delegate void ClosedEventHandler();

    [ExportGroup("Behaviour")]
    [Export] public StringName ToggleAction { get; set; } = "journal_toggle";
    // Ставить игру на паузу, пока журнал открыт
    [Export] public bool PauseGame { get; set; } = true;
    [Export] public JournalSection StartSection { get; set; } = JournalSection.Notes;

    [ExportGroup("Bookmarks")]
    // Контейнер, в котором лежат JournalBookmark (ищутся среди прямых детей)
    [Export] public Control BookmarkContainer { get; set; }

    [ExportGroup("Contents Page")]
    [Export] public Control ContentsPage { get; set; }
    [Export] public Label ContentsTitle { get; set; }
    [Export] public Container ContentsList { get; set; }
    [Export] public Control ContentsEmptyLabel { get; set; }

    [ExportGroup("Entry Page")]
    [Export] public Control EntryPage { get; set; }
    [Export] public Label EntryTitle { get; set; }
    [Export] public TextureRect EntryIllustration { get; set; }
    [Export] public RichTextLabel EntryText { get; set; }
    [Export] public ScrollContainer EntryScroll { get; set; }
    [Export] public BaseButton BackButton { get; set; }
    [Export] public BaseButton PrevButton { get; set; }
    [Export] public BaseButton NextButton { get; set; }
    [Export] public Label PageNumber { get; set; }

    [ExportGroup("Texts")]
    [Export] public string NotesTitle { get; set; } = "Записки";
    [Export] public string ItemsTitle { get; set; } = "Предметы";
    [Export] public string UnreadMarker { get; set; } = "!";
    [Export] public Color UnreadColor { get; set; } = new(0.75f, 0.10f, 0.05f);

    public bool IsOpen => Visible;
    public JournalSection CurrentSection { get; private set; }
    /// <summary>Открытая запись; null — показано оглавление.</summary>
    public JournalEntry CurrentEntry { get; private set; }

    private readonly List<JournalBookmark> _bookmarks = new();
    private JournalManager _journal;
    private Input.MouseModeEnum _prevMouseMode;
    private bool _prevPaused;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // журнал работает, пока игра на паузе
        Visible = false;
        CurrentSection = StartSection;

        if (BookmarkContainer is not null)
        {
            foreach (var child in BookmarkContainer.GetChildren())
            {
                if (child is not JournalBookmark bookmark)
                    continue;
                _bookmarks.Add(bookmark);
                bookmark.Pressed += () => ShowContents(bookmark.Section);
            }
        }

        if (BackButton is not null) BackButton.Pressed += () => ShowContents(CurrentSection);
        if (PrevButton is not null) PrevButton.Pressed += () => Flip(-1);
        if (NextButton is not null) NextButton.Pressed += () => Flip(+1);

        _journal = JournalManager.Instance;
        if (_journal is null)
        {
            GD.PushError("JournalUI: autoload JournalManager не найден. Добавь Scripts/Journal/JournalManager.cs в Autoload.");
            return;
        }
        _journal.EntryAdded += OnEntryAdded;
        _journal.UnreadChanged += RefreshBookmarks;
        _journal.Reloaded += OnReloaded;
        RefreshBookmarks();
    }

    public override void _ExitTree()
    {
        if (_journal is null || !IsInstanceValid(_journal))
            return;
        _journal.EntryAdded -= OnEntryAdded;
        _journal.UnreadChanged -= RefreshBookmarks;
        _journal.Reloaded -= OnReloaded;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(ToggleAction))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (IsOpen && @event.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    // --- Открытие / закрытие ---

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // <summary>Открыть журнал. section == null — последний открытый раздел.</summary>
    public void Open(JournalSection? section = null)
    {
        if (!IsOpen)
        {
            _prevMouseMode = Input.MouseMode;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            if (PauseGame)
            {
                _prevPaused = GetTree().Paused;
                GetTree().Paused = true;
            }
            Visible = true;
            EmitSignal(SignalName.Opened);
        }
        ShowContents(section ?? CurrentSection);
    }

    // <summary>Открыть журнал сразу на странице записи (например, «Прочитать сейчас» после подбора).</summary>
    public void Open(JournalEntry entry)
    {
        Open(entry.Section);
        ShowEntry(entry);
    }

    public void Close()
    {
        if (!IsOpen)
            return;

        Visible = false;
        if (PauseGame)
            GetTree().Paused = _prevPaused;
        Input.MouseMode = _prevMouseMode;
        EmitSignal(SignalName.Closed);
    }

    // --- Страницы ---

    /// <summary>Первая страница раздела — оглавление.</summary>
    public void ShowContents(JournalSection section)
    {
        CurrentSection = section;
        CurrentEntry = null;

        if (ContentsPage is not null) ContentsPage.Visible = true;
        if (EntryPage is not null) EntryPage.Visible = false;
        if (ContentsTitle is not null) ContentsTitle.Text = Tr(SectionTitle(section));

        RebuildContents();
        RefreshBookmarks();
    }

    public void ShowEntry(JournalEntry entry)
    {
        if (entry is null || _journal is null || !_journal.Has(entry))
            return;

        CurrentSection = entry.Section;
        CurrentEntry = entry;

        if (ContentsPage is not null) ContentsPage.Visible = false;
        if (EntryPage is not null) EntryPage.Visible = true;

        if (EntryTitle is not null)
            EntryTitle.Text = Tr(entry.Title);

        if (EntryIllustration is not null)
        {
            var picture = entry.Illustration ?? (entry.Section == JournalSection.Items ? entry.Icon : null);
            EntryIllustration.Texture = picture;
            EntryIllustration.Visible = picture is not null;
        }

        if (EntryText is not null)
            EntryText.Text = Tr(entry.Description);
        if (EntryScroll is not null)
            EntryScroll.ScrollVertical = 0;

        UpdatePageNavigation();
        _journal.MarkRead(entry);   // снимает «!» → UnreadChanged → RefreshBookmarks
        RefreshBookmarks();
    }

    /// <summary>Листать записи раздела: -1 — назад, +1 — вперёд.</summary>
    public void Flip(int direction)
    {
        if (CurrentEntry is null || _journal is null)
            return;

        var entries = _journal.GetEntries(CurrentSection);
        int index = entries.IndexOf(CurrentEntry) + direction;
        if (index >= 0 && index < entries.Count)
            ShowEntry(entries[index]);
    }

    private void UpdatePageNavigation()
    {
        var entries = _journal.GetEntries(CurrentSection);
        int index = entries.IndexOf(CurrentEntry);

        if (PageNumber is not null) PageNumber.Text = $"{index + 1} / {entries.Count}";
        if (PrevButton is not null) PrevButton.Disabled = index <= 0;
        if (NextButton is not null) NextButton.Disabled = index >= entries.Count - 1;
    }

    private void RebuildContents()
    {
        if (ContentsList is null || _journal is null)
            return;

        foreach (var child in ContentsList.GetChildren())
        {
            ContentsList.RemoveChild(child);
            child.QueueFree();
        }

        var entries = _journal.GetEntries(CurrentSection);
        if (ContentsEmptyLabel is not null)
            ContentsEmptyLabel.Visible = entries.Count == 0;

        Control first = null;
        for (int i = 0; i < entries.Count; i++)
        {
            var link = CreateContentsLink(entries[i], i + 1, out var button);
            ContentsList.AddChild(link);
            first ??= button;
        }

        // для геймпада/клавиатуры: фокус на первую ссылку
        if (IsOpen && first is not null)
            first.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Строка оглавления: [!] [иконка  N. Название]. Тема — вариация «JournalContentsLink».</summary>
    private Control CreateContentsLink(JournalEntry entry, int number, out Button button)
    {
        var row = new HBoxContainer();

        var marker = new Label
        {
            Text = _journal.IsUnread(entry) ? UnreadMarker : "",
            CustomMinimumSize = new Vector2(16, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        marker.AddThemeColorOverride("font_color", UnreadColor);
        row.AddChild(marker);

        button = new Button
        {
            Text = $"{number}. {Tr(entry.Title)}",
            Icon = entry.Icon,
            Flat = true,
            Alignment = HorizontalAlignment.Left,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = "JournalContentsLink",
        };
        button.AddThemeConstantOverride("icon_max_width", 32);
        button.Pressed += () => ShowEntry(entry);
        row.AddChild(button);

        return row;
    }

    private void RefreshBookmarks()
    {
        foreach (var bookmark in _bookmarks)
        {
            bookmark.SetPressedNoSignal(bookmark.Section == CurrentSection);
            bookmark.SetUnread(_journal?.HasUnread(bookmark.Section) ?? false);
        }
    }

    // --- Реакция на изменения журнала ---

    private void OnEntryAdded(JournalEntry entry)
    {
        if (!IsOpen || entry.Section != CurrentSection)
            return;

        if (CurrentEntry is null)
            RebuildContents();
        else
            UpdatePageNavigation();
    }

    private void OnReloaded()
    {
        if (IsOpen)
            ShowContents(CurrentSection);
        else
            CurrentEntry = null;
    }

    private string SectionTitle(JournalSection section) => section switch
    {
        JournalSection.Items => ItemsTitle,
        _ => NotesTitle,
    };
}
