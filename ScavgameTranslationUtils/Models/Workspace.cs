using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScavgameTranslationUtils.Models;

public class Workspace
{
    public static readonly string BackupsPath = Path.Combine(Program.AppDataPath, "backups");

    private static AppJsonContext CreateContext(int indentSize) => new AppJsonContext(new JsonSerializerOptions()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
        IndentCharacter = ' ',
        IndentSize = indentSize,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });
    
    private Localization _original;
    private Localization _translation;

    private bool _hasPendingChanges;

    private List<string> _mainOrder;
    private List<string> _buildingsOrder;
    private List<string> _moodlesOrder;
    private List<string> _otherOrder;
    private Dictionary<int, List<string>> _characterOrder;
    
    public List<string> Paths { get; private set; }
    
    public string TranslationPath { get; set; }

    // Some translations might benefit from maintaining existing indents
    public int IndentSize
    {
        get => field;
        set
        {
            field = Math.Clamp(value, 2, 4);
            _hasPendingChanges = true;
        }
    } = 4; // EN.json indent

    // Some translations might benefit from maintaining existing indents
    public bool SortKeys
    {
        get => field;
        set
        {
            field = value;
            _hasPendingChanges = true;
        }
    } = false;
    
    private Workspace(Localization original, Localization translation, string translationPath)
    {
        TranslationPath = translationPath;
        
        _original = original;
        _translation = translation;
        Paths = original.GetPaths();
        
        _mainOrder = new List<string>(_original.Main.Keys);
        _buildingsOrder = new List<string>(_original.Buildings.Keys);
        _moodlesOrder = new List<string>(_original.Moodles.Keys);
        _otherOrder = new List<string>(_original.Other.Keys);
        _characterOrder = [];
        
        for (int characterIndex = 0; characterIndex < _original.Character.Count; characterIndex++)
            _characterOrder[characterIndex] = new List<string>(_original.Character[characterIndex].Keys);
    }
    
    public static async Task<Workspace> CreateAsync(string originalPath, string translationPath)
    {
        Localization original, translation;
        var context = CreateContext(4);
        
        await using (var originalStream = File.OpenRead(originalPath))
        {
            // Use a stream reader to handle any potential UTF BOMs
            var streamReader = new StreamReader(originalStream);
            var text = await streamReader.ReadToEndAsync();
            original = JsonSerializer.Deserialize(text, context.Localization) 
                       ?? throw new InvalidOperationException("Failed to load original text.");
        }

        if (File.Exists(translationPath))
        {
            await using (var translationStream = File.OpenRead(translationPath))
            {
                // Use a stream reader to handle any potential UTF BOMs
                var streamReader = new StreamReader(translationStream);
                var text = await streamReader.ReadToEndAsync();
                translation = JsonSerializer.Deserialize(text, context.Localization)
                              ?? throw new InvalidOperationException("Failed to load translation text.");
            }
        }
        else
        {
            await using (var file = File.OpenWrite(translationPath))
            {
                translation = CreateNewTranslation(original);
                await JsonSerializer.SerializeAsync(file, translation, context.Localization);
            }
        }

        return new Workspace(original, translation, translationPath);
    }

    public string? GetOriginalText(string path) => _original.GetTextByPath(path);
    
    public string? GetText(string path) => _translation.GetTextByPath(path);

    public void SetText(string path, string text)
    {
        Program.LogDebug($"Setting translation key {path}");
        _hasPendingChanges = true;
        _translation.SetTextByPath(path, text);
    }

    public string Name
    {
        get => _translation.Name;
        set
        {
            Program.LogDebug($"Setting translation name");
            _hasPendingChanges = true;
            _translation.Name = value;
        }
    }
    
    public string Description
    {
        get => _translation.Description;
        set
        {
            Program.LogDebug($"Setting translation description");
            _hasPendingChanges = true;
            _translation.Description = value;
        }
    }

    private void ResortKeys()
    {
        // Sort the dictionary keys according to EN.json sort to make it easier to review the JSONs directly
        _translation.Main = new OrderedDictionary<string, string>(_translation.Main.OrderBy(x => _mainOrder.IndexOf(x.Key)));
        _translation.Buildings = new OrderedDictionary<string, string>(_translation.Buildings.OrderBy(x => _buildingsOrder.IndexOf(x.Key)));
        _translation.Moodles = new OrderedDictionary<string, string>(_translation.Moodles.OrderBy(x => _moodlesOrder.IndexOf(x.Key)));
        _translation.Other = new OrderedDictionary<string, string>(_translation.Other.OrderBy(x => _otherOrder.IndexOf(x.Key)));
        
        var characterTranslations = new List<OrderedDictionary<string, List<string>>>();

        for (int characterIndex = 0; characterIndex < _translation.Character.Count; characterIndex++)
        {
            if (!_characterOrder.TryGetValue(characterIndex, out var orderToUse))
                characterTranslations.Add(_translation.Character[characterIndex]);
            else
                characterTranslations.Add(new OrderedDictionary<string, List<string>>(_translation.Character[characterIndex].OrderBy(x => orderToUse.IndexOf(x.Key))));
        }

        _translation.Character = characterTranslations;
    }

    public async Task SaveChanges(bool force = false)
    {
        if (!force && !_hasPendingChanges)
            return;
        
        if (SortKeys)
            ResortKeys();
        
        await using var translationStream = File.Open(TranslationPath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(translationStream, _translation, CreateContext(IndentSize).Localization);

        _hasPendingChanges = false;
        Program.LogDebug("Translation saved!");
    }

    private static string GetSanitizedCurrentTime()
    {
        var time = DateTime.Now.ToString("s");
        
        foreach (var ch in Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()))
        {
            time = time.Replace(ch, '-');
        }

        return time;
    }

    public async Task SaveBackup(bool periodic = false)
    {
        var backupsPathToUse = BackupsPath;

        if (periodic)
            backupsPathToUse = Path.Combine(backupsPathToUse, "periodic_backups");
        
        Directory.CreateDirectory(backupsPathToUse);
        
        var translationName = Path.GetFileNameWithoutExtension(TranslationPath);
        var translationDirectory = Path.GetDirectoryName(TranslationPath)!;
        
        // Hashing is used to allow two translations with identical names to have different backups
        using SHA256 sHA = SHA256.Create();
        var pathHash = BitConverter.ToString(sHA.ComputeHash(Encoding.UTF32.GetBytes(translationDirectory))).Replace("-", "").Substring(0, 8);
        var backupPath = Path.Combine(backupsPathToUse, $"{translationName}-{pathHash}-{GetSanitizedCurrentTime()}-backup.json");
        
        Program.LogDebug($"Creating {(periodic ? "periodic ": "" )}backup {backupPath}");

        await using (var translationStream = File.Open(backupPath, FileMode.Create, FileAccess.Write))
        {
            await JsonSerializer.SerializeAsync(translationStream, _translation, CreateContext(IndentSize).Localization);
        }

        var maxBackups = 3;
        var backupsToDelete =
            Directory.EnumerateFiles(backupsPathToUse, $"{translationName}-{pathHash}-*-backup.json")
                .OrderDescending()
                .Skip(maxBackups);

        foreach (var backup in backupsToDelete)
        {
            Program.LogDebug($"Deleting backup {backup}");
            File.Delete(backup);
        }
    }

    private static Localization CreateNewTranslation(Localization original)
    {
        var translation = new Localization()
        {
            Name = "New Translation",
            Description = "Translation created with ScavgameTranslationUtils",
        };
        
        // Some keys should be kept intact between locales, such as fonts and sprites
        foreach (var key in original.GetPaths())
        {
            bool shouldCopy =
                key.StartsWith("notes:") && key.EndsWith(":font")
                || (key.StartsWith("notes:") || key.StartsWith("pdaNotes:")) && key.EndsWith(":sprite");
            
            if (shouldCopy)
            {
                var value = original.GetTextByPath(key);
                
                if (value != null)
                    translation.SetTextByPath(key, value);
            }
        }

        return translation;
    }
}