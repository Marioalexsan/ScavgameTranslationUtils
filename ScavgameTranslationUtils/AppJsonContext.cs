using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils;

[JsonSerializable(typeof(Localization))]
[JsonSerializable(typeof(AppSettings))]
public partial class AppJsonContext : JsonSerializerContext
{
    public static AppJsonContext CreateContext(int indentSize) => new AppJsonContext(new JsonSerializerOptions()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = indentSize,
        AllowTrailingCommas = true, // To deal with some specific old locales
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });
}