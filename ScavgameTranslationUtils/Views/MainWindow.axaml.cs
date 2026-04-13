using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
    // TODO: Better pattern?
    public MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public static List<CultureInfo> KnownCultures =
        CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where(x => Regex.IsMatch(x.Name, "^[a-z]{2}(?:-[A-Z]{2})?$"))
            .ToList();

    public static List<string> TranslationNames { get; } =
        KnownCultures
            .Select(x => $"{x.DisplayName} [{x.Name}]")
            .ToList();
    
    public static List<string> TranslationCountryCodes { get; } =
        KnownCultures
            .Select(x => x.Name)
            .ToList();

    public int SelectedCountryCode { get; set; } = -1;
    
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenOriginalLocalization(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null)
            return;

        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Select Casualties: Unknown executable or EN.json",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("EN.json or game executable")
                    {
                        Patterns = ["EN.json", "CasualtiesUnknown.exe"]
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
            var selectedIndex = CountryCodeComboBox.SelectedIndex;

            var selectedName = 0 <= selectedIndex && selectedIndex < TranslationCountryCodes.Count
                ? TranslationCountryCodes[SelectedCountryCode]
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
            DataContext = new TranslationWindowViewModel(ViewModel.Workspace)
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