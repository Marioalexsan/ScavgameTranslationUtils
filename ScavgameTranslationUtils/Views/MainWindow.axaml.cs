using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ScavgameTranslationUtils.Models;
using ScavgameTranslationUtils.ViewModels;

namespace ScavgameTranslationUtils.Views;

public partial class MainWindow : Window
{
    public record SimpleCultureInfo(string DisplayName, string Name);
    
    // TODO: Better pattern?
    public MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;
    
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenGameExecutable(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select Casualties: Unknown executable",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("CasualtiesUnknown.exe")
                    {
                        Patterns = ["CasualtiesUnknown.exe"]
                    },
                    new FilePickerFileType("Any executable")
                    {
                        Patterns = ["*.exe"]
                    }
                ]
            });

            if (files.Count == 0)
                return;

            await ViewModel.OpenGameAssets(files[0].Path.LocalPath);
        });
    }

    private void OpenOriginalLocalization(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select EN.json or Casualties: Unknown executable",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    // For some reason, grouping EN.json and CasualtiesUnknown.exe under one list doesn't work correctly
                    new FilePickerFileType("EN.json")
                    {
                        Patterns = ["EN.json"]
                    },
                    new FilePickerFileType("Game executable")
                    {
                        Patterns = ["CasualtiesUnknown.exe"]
                    },
                    new FilePickerFileType("Any file")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files.Count == 0)
                return;

            await ViewModel.OpenOriginalTranslation(files[0].Path.LocalPath);
            ViewModel.SelectedEnglishVersion = null;
        });
    }

    private void OpenTranslationFile(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select translation file",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Translation file (JSON)")
                    {
                        Patterns = ["*.json"]
                    },
                    new FilePickerFileType("Any file")
                    {
                        Patterns = ["*.*"]
                    }
                ]
            });

            if (files.Count == 0)
                return;

            await ViewModel.OpenTranslationFile(files[0].Path.LocalPath);
        });
    }
    
    private void CreateTranslationFile(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var selectedName = 0 <= ViewModel.SelectedCountryCode && ViewModel.SelectedCountryCode < ViewModel.TranslationCountryCodes.Count
                ? ViewModel.TranslationCountryCodes[ViewModel.SelectedCountryCode]
                : "lang-COUNTRY";

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Select where to save the new translation",
                SuggestedFileName = $"{selectedName}.json",
                FileTypeChoices =
                [
                    new FilePickerFileType("Translation files")
                    {
                        Patterns = ["*.json"]
                    },
                    new FilePickerFileType("Any file")
                    {
                        Patterns = ["*.*"]
                    }
                ],
                ShowOverwritePrompt = true
            });

            if (file == null)
                return;

            await ViewModel.OpenTranslationFile(file.Path.LocalPath);
        });
    }

    private async void BeginTranslation(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || ViewModel.Workspace == null)
            return;

        var workspace = ViewModel.Workspace;

        await Task.Run(() => workspace.SaveBackup());

        var translationWindow = new TranslationWindow()
        {
            DataContext = new TranslationWindowViewModel(ViewModel.GameAssets, ViewModel.Workspace)
        };
        Hide();

        translationWindow.Closing += (_, _) =>
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ViewModel.Workspace = null;
                ViewModel.TranslationPath = null;
            });
            Show();
        };
        translationWindow.Show();
    }

    private void OpenBackupsFolder(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await Launcher.LaunchUriAsync(new Uri(Workspace.BackupsPath));
        });
    }
}