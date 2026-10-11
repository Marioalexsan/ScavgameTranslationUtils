using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScavgameTranslationUtils.Models;
using ScavgameTranslationUtils.Views;

namespace ScavgameTranslationUtils.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public const string LocalFile = "Local file";
    public const int PathMaxViewableLength = 50;
    
    public MainWindowViewModel()
    {
        var baseCultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .ToList();

        var cultureList = baseCultures
            .Select(x => new MainWindow.SimpleCultureInfo(x.DisplayName, x.Name))
            .ToList();

        cultureList.RemoveAll(x => x.Name == ""); // Don't need the invariant culture

        AliasLocale("zh-Hans-CN", "zh-CN"); // Uses simplified system in scavgame-locale
        AliasLocale("zh-Hans-SG", "zh-SG"); // Assuming to be simplified
        AliasLocale("zh-Hant-MO", "zh-MO"); // Assuming to be traditional
        AliasLocale("zh-Hant-HK", "zh-HK"); // Assuming to be traditional
        AliasLocale("zh-Hant-TW", "zh-TW"); // Uses traditional system in scavgame-locale

        foreach (var culture in cultureList.OrderBy(x => x.DisplayName))
            KnownCultures.Add(culture);
        
        foreach (var translationName in KnownCultures.Select(x => $"{x.DisplayName} [{x.Name}]"))
            TranslationNames.Add(translationName);
        
        foreach (var translationName in KnownCultures.Select(x => x.Name))
            TranslationCountryCodes.Add(translationName);

        void AliasLocale(string original, string alias)
        {
            var locale = cultureList.Find(x => x.Name == original);
        
            if (locale != null)
                cultureList.Add(new MainWindow.SimpleCultureInfo(locale.DisplayName, alias));
        }
    }
    
    public ObservableCollection<string> KnownEnglishVersions { get; } = [];

    public ObservableCollection<MainWindow.SimpleCultureInfo> KnownCultures { get; } = [];

    public ObservableCollection<string> TranslationNames { get; } = [];

    public ObservableCollection<string> TranslationCountryCodes { get; } = [];

    public bool CanTranslate => Workspace != null;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameExecutablePathTruncated))]
    private string? _gameExecutablePath;

    public string? GameExecutablePathTruncated
    {
        get
        {
            if (GameExecutablePath == null)
                return null;
            
            if (GameExecutablePath.Length >= PathMaxViewableLength)
                return "[...]" + GameExecutablePath[^PathMaxViewableLength..];

            return GameExecutablePath;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EnglishTranslationPathTruncated))]
    private string? _englishTranslationPath;

    public string? EnglishTranslationPathTruncated
    {
        get
        {
            if (EnglishTranslationPath == null)
                return null;
            
            if (EnglishTranslationPath.Length >= PathMaxViewableLength)
                return "[...]" + EnglishTranslationPath[^PathMaxViewableLength..];

            return EnglishTranslationPath;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TranslationPathTruncated))]
    private string? _translationPath;

    public string? TranslationPathTruncated
    {
        get
        {
            if (TranslationPath == null)
                return null;
            
            if (TranslationPath.Length >= PathMaxViewableLength)
                return "[...]" + TranslationPath[^PathMaxViewableLength..];

            return TranslationPath;
        }
    }

    public bool HasErrors => GameAssetsHaveErrors || WorkspaceHasErrors;
    public string ErrorMessage
    {
        get
        {
            var errors = "";

            if (GameAssetsHaveErrors)
                errors += "Game assets couldn't be loaded! Is the game install valid?\n";

            if (WorkspaceHasErrors)
                errors += "Couldn't create workspace! Are the translation files valid?\n";

            return errors.Trim('\n');
        }
    }
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    private bool _gameAssetsHaveErrors;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors))]
    [NotifyPropertyChangedFor(nameof(ErrorMessage))]
    private bool _workspaceHasErrors;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTranslate))]
    private Workspace? _workspace;

    [ObservableProperty]
    private GameAssets? _gameAssets;

    [ObservableProperty]
    public int _selectedCountryCode = -1;

    [ObservableProperty]
    public string? _selectedEnglishVersion;

    partial void OnSelectedEnglishVersionChanged(string? value)
    {
        if (value == null || value == LocalFile)
            return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await OpenOriginalTranslation(Constants.GetEnglishLocaleVersionPath(value));
        });
    }

    [RelayCommand]
    public async Task OpenOriginalTranslation(string translationPath)
    {
        if (translationPath.EndsWith("CasualtiesUnknown.exe"))
        {
            // TODO: Is this Windows-specific?
            translationPath = Path.Combine(
                Path.GetDirectoryName(translationPath)!,
                "CasualtiesUnknown_Data", 
                "Lang", 
                "EN.json");
        }
        
        Program.Settings.EnglishTranslationPath = EnglishTranslationPath = translationPath;
        await AppSettings.SaveAsync(Program.Settings);

        await TryPrepareWorkspace();
        
        if (GameExecutablePath == null && Workspace != null)
        {
            // Is this from the game install?
            var gameFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(translationPath)));

            if (gameFolder != null)
            {
                var gameExecutablePath = Path.Combine(gameFolder, "CasualtiesUnknown.exe");

                if (File.Exists(gameExecutablePath))
                {
                    GameExecutablePath = gameExecutablePath;
                    await TryLoadGameAssets();
                }
            }
        }
    }

    public async Task OpenGameAssets(string gameExecutablePath)
    {
        Program.Settings.GameExecutablePath = GameExecutablePath = gameExecutablePath;
        await AppSettings.SaveAsync(Program.Settings);
        await TryLoadGameAssets();
    }
    
    public async Task OpenTranslationFile(string translationPath)
    {
        TranslationPath = translationPath;
        await TryPrepareWorkspace();
    }

    private async Task TryLoadGameAssets()
    {
        GameAssetsHaveErrors = false;
        GameAssets?.Dispose();
        GameAssets = null;
        
        if (GameExecutablePath == null)
            return;

        var gameDirectory = Path.GetDirectoryName(GameExecutablePath);

        if (gameDirectory == null)
        {
            GameAssetsHaveErrors = true;
            return;
        }
        
        var dataFolder = Path.Combine(gameDirectory, "CasualtiesUnknown_Data");

        // TODO: Sync with LoadFrom's internals
        bool resourcesOk =
            File.Exists(Path.Combine(dataFolder, "globalgamemanagers"))
            && File.Exists(Path.Combine(dataFolder, "globalgamemanagers.assets"))
            && File.Exists(Path.Combine(dataFolder, "resources.assets"));

        if (!resourcesOk)
        {
            GameAssetsHaveErrors = true;
            return;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            Program.LogDebug($"Loading assets from {dataFolder}...");
            
            GameAssets = new GameAssets();
            GameAssets.LoadFrom(dataFolder);
            
            Program.LogDebug($"Loaded game assets in {stopwatch.ElapsedMilliseconds} ms!");
        }
        catch (Exception e)
        {
            GameAssetsHaveErrors = true;
            GameAssets?.Dispose();
            GameAssets = null;
            Program.LogDebug("Encountered an error while loading game assets!");
            Program.LogDebug(e.ToString());
        }
    }

    private async Task TryPrepareWorkspace()
    {
        WorkspaceHasErrors = false;

        if (Workspace != null)
            await Workspace.SaveChanges();

        Workspace = null;
        
        if (EnglishTranslationPath == null || TranslationPath == null)
            return;

        try
        {
            Workspace = await Workspace.CreateAsync(EnglishTranslationPath, TranslationPath);
        }
        catch (Exception e)
        {
            WorkspaceHasErrors = true;
            Program.LogDebug("Encountered an error while preparing workspace!");
            Program.LogDebug(e.ToString());
        }
    }
}