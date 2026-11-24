using Godot;
using System;
using System.Collections.Generic;

namespace SteelAndSalt.Menus;

public partial class MenuSettings : Control
{
    // Путь к файлу настроек
    private const string SettingsPath = "user://settings.cfg";

    // Индексы аудио-шин (должны совпадать с Audio Bus Layout)
    private const int BusMaster = 0;
    private const int BusSFX = 1;
    private const int BusMusic = 2;

    // Маппинг: имя действия InputMap → кнопка UI
    private Dictionary<string, Button> _keyButtons = new();
    private string _waitingForAction = null; // Какое действие сейчас переназначаем

    // Узлы UI
    private Slider _sliderMaster;
    private Slider _sliderSFX;
    private Slider _sliderMusic;
    private Slider _sliderGamma;
    private ColorRect _gammaRect;
    [Export] private OptionButton _languageOption;

    // Значения по умолчанию
    private float _defaultMaster = 0.8f;
    private float _defaultSFX = 0.8f;
    private float _defaultMusic = 0.8f;
    private float _defaultGamma = 1.0f;

    public override void _Ready()
    {
        // Собираем ссылки на кнопки переназначения
        _keyButtons["move_forward"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer/Button");
        _keyButtons["move_back"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer2/Button");
        _keyButtons["move_left"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer3/Button");
        _keyButtons["move_right"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer4/Button");
        _keyButtons["interact"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer5/Button");
        _keyButtons["inventory"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer6/Button");
        _keyButtons["journal"] = GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_CONTROLS/HBoxContainer7/Button");

        // Слайдеры
        _sliderMaster = GetNode<Slider>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_SOUND/HBoxContainer/HSlider");
        _sliderSFX = GetNode<Slider>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_SOUND/HBoxContainer3/HSlider");
        _sliderMusic = GetNode<Slider>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_SOUND/HBoxContainer2/HSlider");
        _sliderGamma = GetNode<Slider>("PanelContainer/MarginContainer/VBoxContainer/TabContainer/MENU_SETTINGS_VIDEO/HBoxContainer/HSlider");

        // Гамма-оверлей
        _gammaRect = GetNodeOrNull<ColorRect>("GammaOverlay/GammaRect");

        // Подписываем кнопки на переназначение
        foreach (var kvp in _keyButtons)
        {
            kvp.Value.Pressed += () => StartRebinding(kvp.Key);
        }

        // Кнопки сохранения/отмены
        GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/HBoxContainer/Button").Pressed += OnSavePressed;
        GetNode<Button>("PanelContainer/MarginContainer/VBoxContainer/HBoxContainer/Button2").Pressed += CancelChanges;

        _languageOption.ItemSelected += OnLanguageSelected;
        FillLanguages();

    // Загружаем настройки
        LoadSettings();
        ApplySettings();
    }

    // === Смена языка ===
    private void FillLanguages()
    {
        _languageOption.Clear();
        string current = TranslationServer.GetLocale(); // может быть "ru_RU"

        for (int i = 0; i < LocalizationManager.SupportedLocales.Length; i++)
        {
            string code = LocalizationManager.SupportedLocales[i];
            string name = LocalizationManager.NativeNames.GetValueOrDefault(code, code);

            _languageOption.AddItem(name);
            _languageOption.SetItemMetadata(i, code);

            if (current == code || current.StartsWith(code + "_"))
                _languageOption.Select(i); // Select() не вызывает ItemSelected — рекурсии нет
        }
    }

    private void OnLanguageSelected(long index)
    {
        string code = _languageOption.GetItemMetadata((int)index).AsString();
        LocalizationManager.Instance.SetLocale(code);
        // Ничего больше обновлять не нужно: всё меню перерисуется само.
    }

    // === ПЕРЕНАЗНАЧЕНИЕ КЛАВИШ ===

    private void StartRebinding(string action)
    {
        _waitingForAction = action;
        _keyButtons[action].Text = "...";
        // Захватываем ввод, чтобы другие узлы не реагировали
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_waitingForAction == null) return;

        // Игнорируем движение мыши и релизы клавиш
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            // Переназначаем действие
            InputMap.ActionEraseEvents(_waitingForAction);
            InputMap.ActionAddEvent(_waitingForAction, keyEvent);

            // Обновляем текст кнопки
            _keyButtons[_waitingForAction].Text = keyEvent.AsText();
            _waitingForAction = null;

            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
        {
            InputMap.ActionEraseEvents(_waitingForAction);
            InputMap.ActionAddEvent(_waitingForAction, mouseEvent);

            _keyButtons[_waitingForAction].Text = mouseEvent.AsText();
            _waitingForAction = null;

            GetViewport().SetInputAsHandled();
        }
    }

    // === СОХРАНЕНИЕ / ЗАГРУЗКА ===

    private void OnSavePressed()
    {
        SaveSettings();
        ApplySettings();
        Hide(); // Закрываем меню после сохранения
    }

    public void CancelChanges()
    {
        // Возвращаем значения из файла (отменяем изменения)
        LoadSettings();
        ApplySettings();
        Hide();
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();
        config.Load(SettingsPath);

        // Звук
        config.SetValue("audio", "master", _sliderMaster.Value);
        config.SetValue("audio", "sfx", _sliderSFX.Value);
        config.SetValue("audio", "music", _sliderMusic.Value);

        // Гамма
        config.SetValue("display", "gamma", _sliderGamma.Value);

        // Клавиши — сохраняем строковое представление события
        foreach (var kvp in _keyButtons)
        {
            var events = InputMap.ActionGetEvents(kvp.Key);
            if (events.Count > 0)
            {
                config.SetValue("controls", kvp.Key, events[0].AsText());
            }
        }

        config.Save(SettingsPath);
        GD.Print("Настройки сохранены: " + SettingsPath);
    }

    private void LoadSettings()
    {
        var config = new ConfigFile();
        var err = config.Load(SettingsPath);

        if (err != Error.Ok)
        {
            GD.Print("Файл настроек не найден, используем значения по умолчанию");
            return;
        }

        // Звук
        _sliderMaster.Value = (double)config.GetValue("audio", "master", _defaultMaster);
        _sliderSFX.Value = (double)config.GetValue("audio", "sfx", _defaultSFX);
        _sliderMusic.Value = (double)config.GetValue("audio", "music", _defaultMusic);

        // Гамма
        _sliderGamma.Value = (double)config.GetValue("display", "gamma", _defaultGamma);

        // Клавиши
        foreach (var kvp in _keyButtons)
        {
            string keyText = (string)config.GetValue("controls", kvp.Key, "");

            if (!string.IsNullOrEmpty(keyText))
            {
                // Ключ есть в файле — пытаемся восстановить
                // (подробнее см. ниже)
                kvp.Value.Text = keyText;
            }
            else
            {
                // Ключа нет — берём текущее значение из InputMap
                var events = InputMap.ActionGetEvents(kvp.Key);
                if (events.Count > 0)
                {
                    kvp.Value.Text = events[0].AsText();
                }
            }
        }
    }

    // === ПРИМЕНЕНИЕ НАСТРОЕК ===

    private void ApplySettings()
    {
        // Громкость (переводим 0..1 в децибелы)
        AudioServer.SetBusVolumeDb(BusMaster, LinearToDb((float)_sliderMaster.Value));
        AudioServer.SetBusVolumeDb(BusSFX, LinearToDb((float)_sliderSFX.Value));
        AudioServer.SetBusVolumeDb(BusMusic, LinearToDb((float)_sliderMusic.Value));

        // Гамма — через оверлей (универсальный способ для 2D/3D)
        // Значение 1.0 = без изменений, 0.5 = темнее, 1.5 = светлее
        float gamma = (float)_sliderGamma.Value;
        float overlayAlpha = Mathf.Clamp(1.0f - gamma, 0.0f, 0.5f);
        if (_gammaRect != null)
            _gammaRect.Color = new Color(0, 0, 0, overlayAlpha);
    }

    private float LinearToDb(float linear)
    {
        if (linear <= 0.0001f) return -80.0f;
        return 20.0f * Mathf.Log(linear) / Mathf.Log(10.0f);
    }
}
