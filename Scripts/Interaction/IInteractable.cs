using Godot;

namespace SteelAndSalt.Interaction;

/// <summary>Вид взаимодействия — определяет иконку/подсказку по умолчанию и реакцию игрока (анимация, звук).</summary>
public enum InteractionType
{
    Use,        // универсальное «Использовать»
    PickUp,     // подобрать предмет
    Open,       // открыть дверь / сундук
    Talk,       // диалог с NPC
    Examine,    // осмотреть
    Toggle,     // рычаг, переключатель
    Sit,        // сесть / занять место (стул, штурвал)
    Loot,       // обыскать контейнер / тело
    Craft,      // верстак, станок
}

/// <summary>Как активируется взаимодействие.</summary>
public enum InteractionTrigger
{
    Press,      // мгновенно по нажатию
    Hold,       // удерживать кнопку HoldDuration секунд
}

/// <summary>Результат попытки взаимодействия.</summary>
public enum InteractionResult
{
    Success,
    Failed,     // логика отработала, но неудачно (заперто, нет ключа)
    Blocked,    // CanInteract == false
}

/// <summary>Данные для UI и системы взаимодействия.</summary>
public readonly record struct InteractionData(
    InteractionType Type,
    InteractionTrigger Trigger,
    string PromptText,
    Texture2D Icon = null,
    float HoldDuration = 0f,
    int Priority = 0)           // при нескольких объектах в фокусе выбирается наибольший
{
    public bool RequiresHold => Trigger == InteractionTrigger.Hold && HoldDuration > 0f;
}

/// <summary>Контекст конкретного взаимодействия: кто и откуда.</summary>
public readonly record struct InteractionContext(Node Interactor, Vector3 InteractorPosition);

/// <summary>
/// Интерфейс интерактивного игрового объекта.
/// Обязательны: GetInteractionData, CanInteract, Interact.
/// Хуки фокуса и удержания имеют реализацию по умолчанию — переопределяй по необходимости.
/// </summary>
public interface IInteractable
{
    /// <summary>Вид взаимодействия (дублирует Data.Type для быстрых проверок/фильтрации).</summary>
    InteractionType InteractionType { get; }
    /// <summary>Данные для UI. Принимает interactor, т.к. подсказка может зависеть от игрока («Открыть» vs «Заперто»).</summary>
    InteractionData GetInteractionData(Node interactor);

    /// <summary>Можно ли взаимодействовать сейчас. reason — текст для UI при отказе.</summary>
    bool CanInteract(Node interactor, out string reason);

    /// <summary>Основное действие. Вызывается после успешного Press или завершения Hold.</summary>
    InteractionResult Interact(in InteractionContext context);

    // --- Необязательные хуки ---

    /// <summary>Объект попал в фокус (подсветка, outline).</summary>
    void OnFocusEntered(Node interactor) { }

    /// <summary>Объект вышел из фокуса.</summary>
    void OnFocusExited(Node interactor) { }

    /// <summary>Прогресс удержания 0..1 (для Hold).</summary>
    void OnHoldProgress(Node interactor, float progress) { }

    /// <summary>Удержание прервано (отпустил кнопку / потерял фокус).</summary>
    void OnHoldCanceled(Node interactor) { }
}
