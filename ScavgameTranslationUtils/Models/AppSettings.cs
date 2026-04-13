using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScavgameTranslationUtils.Models;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(Program.AppDataPath, "settings.json");

    [JsonPropertyName("englishTranslationPath")]
    public string? EnglishTranslationPath { get; set; }

    public static async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        using var stream = File.OpenRead(SettingsPath);
        return await JsonSerializer.DeserializeAsync(stream, AppJsonContext.Default.AppSettings) ?? new AppSettings();
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);

        using var stream = File.Open(SettingsPath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, settings, AppJsonContext.Default.AppSettings);
    }
}