using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ScavgameTranslationUtils.Models;

public class Workspace
{
    public static readonly string BackupsPath = Path.Combine(Program.AppDataPath, "backups");

    private Localization _original;
    private Localization _translation;

    private bool _hasPendingChanges;
    
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
    }
    
    public static async Task<Workspace> CreateAsync(string originalPath, string translationPath)
    {
        Localization original, translation;
        
        await using (var originalStream = File.OpenRead(originalPath))
        {
            original = Localization.LoadFromStream(originalStream)
                       ?? throw new InvalidOperationException("Failed to load original text.");
        }

        if (File.Exists(translationPath))
        {
            await using (var translationStream = File.OpenRead(translationPath))
            {
                translation = Localization.LoadFromStream(translationStream)
                              ?? throw new InvalidOperationException("Failed to load translation text.");
            }
        }
        else
        {
            await using (var file = File.OpenWrite(translationPath))
            {
                translation = CreateNewTranslation(original);
                translation.SaveToStream(file, AppJsonContext.CreateWriterOptions(4));
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
        get => _translation.Name ?? "";
        set
        {
            Program.LogDebug($"Setting translation name");
            _hasPendingChanges = true;
            _translation.Name = value;
        }
    }
    
    public string Description
    {
        get => _translation.Description ?? "";
        set
        {
            Program.LogDebug($"Setting translation description");
            _hasPendingChanges = true;
            _translation.Description = value;
        }
    }
    
    public string Version
    {
        get => _translation.Version ?? "";
        set
        {
            Program.LogDebug($"Setting translation version");
            _hasPendingChanges = true;
            _translation.Version = value;
        }
    }

    public async Task SaveChanges(bool force = false)
    {
        if (!force && !_hasPendingChanges)
            return;

        if (SortKeys)
            _translation.CopyOrderFrom(_original);
        
        await using var translationStream = File.Open(TranslationPath, FileMode.Create, FileAccess.Write);
        _translation.SaveToStream(translationStream, AppJsonContext.CreateWriterOptions(IndentSize));

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
            _translation.SaveToStream(translationStream, AppJsonContext.CreateWriterOptions(IndentSize));
        }

        var maxBackups = 5;
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

    public void BackfillTranslation()
    {
        var backfilledTranslated = new Localization()
        {
            Name = _translation.Name,
            Description = _translation.Description
        };

        foreach (var path in _original.GetPaths())
        {
            var originalText = _original.GetTextByPath(path);
            var translatedText = _translation.GetTextByPath(path);

            if (originalText != null && (translatedText == null || Constants.IsLikelyUntranslatedEnglish(path, originalText, translatedText)))
            {
                backfilledTranslated.SetTextByPath(path, originalText);
            }
            else if (translatedText != null)
            {
                backfilledTranslated.SetTextByPath(path, translatedText);
            }
        }

        _translation = backfilledTranslated;
    }

    private static Localization CreateNewTranslation(Localization original)
    {
        var translation = new Localization()
        {
            Name = "New Translation (rename this to your language)",
            Description = "Describe your translation in here. Keep in mind to credit any contributors, as this will" +
                          "be shown within the game's locale settings menu.",
            Version = original.Version,
        };
        
        // Some keys should be kept intact between locales, such as fonts and sprites
        foreach (var key in original.GetPaths())
        {
            var value = original.GetTextByPath(key);

            if (value != null && Constants.ShouldRemainIdenticalInTranslation(key, value))
                translation.SetTextByPath(key, value);
        }

        return translation;
    }
}