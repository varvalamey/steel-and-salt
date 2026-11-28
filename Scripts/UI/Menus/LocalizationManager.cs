using Godot;
using System;
using System.Collections.Generic;

public partial class LocalizationManager : Node
{
    private const string ConfigPath = "user://settings.cfg";
    private const string Section = "localization";
    private const string KeyLocale = "locale";

    public static LocalizationManager Instance { get; private set; }

    [Signal] public delegate void LocaleChangedEventHandler(string locale);

    // Языки, которые реально есть в игре. Порядок = порядок в выпадающем списке.
    public static readonly string[] SupportedLocales = { "en", "ru" };

    // Названия языков на самих себе — их НЕ переводят никогда.
    public static readonly Dictionary<string, string> NativeNames = new()
    {
        { "en", "English" },
        { "ru", "Русский" },
    };

    // _EnterTree, а не _Ready: локаль должна примениться до _Ready остальных узлов
    public override void _EnterTree()
    {
        Instance = this;
        TranslationServer.SetLocale(LoadLocale());
    }

    public string CurrentLocale => TranslationServer.GetLocale();

    public void SetLocale(string locale)
    {
        if (string.IsNullOrEmpty(locale))
            return;

        TranslationServer.SetLocale(locale);
        SaveLocale(locale);
        EmitSignal(SignalName.LocaleChanged, locale); // fix
    }

    private string LoadLocale()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(ConfigPath) == Error.Ok)
            return cfg.GetValue(Section, KeyLocale, DetectSystemLocale()).AsString();

        return DetectSystemLocale();
    }

    private void SaveLocale(string locale)
    {
        var cfg = new ConfigFile();
        cfg.Load(ConfigPath); // подхватываем остальные настройки, если они есть
        cfg.SetValue(Section, KeyLocale, locale);
        cfg.Save(ConfigPath);
    }

    private static string DetectSystemLocale()
    {
        string system = OS.GetLocaleLanguage(); // "ru", "en", "de"...
        return Array.IndexOf(SupportedLocales, system) >= 0 ? system : "en";
    }
}
