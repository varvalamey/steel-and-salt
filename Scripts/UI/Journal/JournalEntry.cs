using Godot;

namespace SteelAndSalt.Journal;

public enum JournalSection
{
    Notes,  // записки, письма, документы
    Items,  // предметы
}


// Описание одной записи журнала (записка или предмет).
// создавать как .tres (ПКМ в FileSystem -> New Resource -> JournalEntry),
// чтобы путь ресурса был стабильным — по нему работает сохранение.

[GlobalClass]
public partial class JournalEntry : Resource
{

    [Export] public string Id { get; set; } = "";
    [Export] public JournalSection Section { get; set; } = JournalSection.Notes;
    [Export] public string Title { get; set; } = "";
    // Текст страницы. Поддерживает BBCode
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    // Маленькая иконка в оглавлении
    [Export] public Texture2D Icon { get; set; }
    // Картинка на странице записи. Пусто — для предметов берётся Icon
    [Export] public Texture2D Illustration { get; set; }
    /// <summary>Порядок в оглавлении. Записи с одинаковым значением идут в порядке подбора.</summary>
    [Export] public int SortOrder { get; set; }

    public string Key => string.IsNullOrEmpty(Id) ? ResourcePath : Id;
}
