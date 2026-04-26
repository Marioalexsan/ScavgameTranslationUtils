using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public bool CanTranslate => Workspace != null;
    
    [ObservableProperty]
    private string? _gameExecutablePath;

    [ObservableProperty]
    private string? _englishTranslationPath;

    [ObservableProperty]
    private string? _translationPath;

    // TODO: Use this
    [ObservableProperty]
    private bool _gameAssetsHaveErrors;

    // TODO: Use this
    [ObservableProperty]
    private bool _workspaceHasErrors;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTranslate))]
    private Workspace? _workspace;

    [ObservableProperty]
    private GameAssets? _gameAssets;

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
            // TODO: Better error handling
            GameAssetsHaveErrors = true;
            GameAssets?.Dispose();
            GameAssets = null;
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
            // TODO: Better error handling
            WorkspaceHasErrors = true;
            Program.LogDebug(e.ToString());
        }
    }
}