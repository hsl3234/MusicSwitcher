using System.IO;
using System.Text.Json;

namespace MusicSwitcher.Model;

/// <summary>
/// Настройки виджета: монитор, прозрачность, позиция окна.
/// </summary>
public class WidgetSettings
{
    public int MonitorIndex { get; set; }
    public double Opacity { get; set; } = 1.0;
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public string? BackgroundColor { get; set; }
    public string? VolumeTargetProcessName { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "MusicSwitcher");
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }

    public static WidgetSettings Load()
    {
        var path = GetSettingsPath();
        if (!File.Exists(path))
            return new WidgetSettings();

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<WidgetSettings>(json);
            if (loaded != null)
                return loaded;
        }
        catch
        {
            // ignore corrupted/missing file
        }

        return new WidgetSettings();
    }

    public void Save()
    {
        try
        {
            var path = GetSettingsPath();
            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // ignore write errors
        }
    }
}
