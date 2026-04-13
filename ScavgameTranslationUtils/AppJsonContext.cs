using System.Text.Json.Serialization;
using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils;

[JsonSerializable(typeof(Localization))]
[JsonSerializable(typeof(AppSettings))]
public partial class AppJsonContext : JsonSerializerContext
{
    
}