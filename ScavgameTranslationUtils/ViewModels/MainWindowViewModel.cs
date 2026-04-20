using System;
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
    private string? _englishTranslationPath = Program.Settings.EnglishTranslationPath;

    [ObservableProperty]
    private string? _translationPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTranslate))]
    private Workspace? _workspace;

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
    }

    [RelayCommand]
    public async Task OpenTranslationFile(string translationPath)
    {
        TranslationPath = translationPath;
        await TryPrepareWorkspace();
    }

    private async Task TryPrepareWorkspace()
    {
        if (EnglishTranslationPath == null || TranslationPath == null)
        {
            Workspace = null;
            return;
        }

        if (Workspace != null)
        {
            await Workspace.SaveChanges();
        }

        Workspace = null;

        try
        {
            Workspace = await Workspace.CreateAsync(EnglishTranslationPath, TranslationPath);
        }
        catch (Exception e)
        {
            // TODO: Better error handling
            Program.LogDebug(e.ToString());
        }
    }
}