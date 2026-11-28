using Godot;

namespace SteelAndSalt.Journal;

/// <summary>Раздел журнала, в котором показывается запись (и закладка, к которой она относится).</summary>
public enum JournalSection
{
    Notes,  // записки, письма, документы
    Items,  // предметы
}

/// <summary>
/// Описание одной записи журнала (записка или предмет).
/// Создавать как .tres (ПКМ в FileSystem → New Resource → JournalEntry),
/// чтобы путь ресурса был стабильным — по нему работает сохранение.
/// </summary>
[GlobalClass]
public partial class JournalEntry : Resource
{
    /// <summary>Уникальный ключ записи. Пусто — используется путь ресурса.</summary>
    [Export] public string Id { get; set; } = "";
    [Export] public JournalSection Section { get; set; } = JournalSection.Notes;
    [Export] public string Title { get; set; } = "";
    /// <summary>Текст страницы. Поддерживает BBCode.</summary>
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    /// <summary>Маленькая иконка в оглавлении.</summary>
    [Export] public Texture2D Icon { get; set; }
    /// <summary>Картинка на странице записи. Пусто — для предметов берётся Icon.</summary>
    [Export] public Texture2D Illustration { get; set; }
    /// <summary>Порядок в оглавлении. Записи с одинаковым значением идут в порядке подбора.</summary>
    [Export] public int SortOrder { get; set; }

    public string Key => string.IsNullOrEmpty(Id) ? ResourcePath : Id;
}
