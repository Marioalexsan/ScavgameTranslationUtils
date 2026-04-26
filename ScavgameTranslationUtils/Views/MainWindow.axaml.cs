using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    
    static MainWindow()
    {
        var baseCultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .ToList();

        var cultureList = baseCultures
            .Select(x => new SimpleCultureInfo(x.DisplayName, x.Name))
            .ToList();

        cultureList.RemoveAll(x => x.Name == ""); // Don't need the invariant culture

        AliasLocale("zh-Hans-CN", "zh-CN"); // Uses simplified system in scavgame-locale
        AliasLocale("zh-Hans-SG", "zh-SG"); // Assuming to be simplified
        AliasLocale("zh-Hant-MO", "zh-MO"); // Assuming to be traditional
        AliasLocale("zh-Hant-HK", "zh-HK"); // Assuming to be traditional
        AliasLocale("zh-Hant-TW", "zh-TW"); // Uses traditional system in scavgame-locale

        KnownCultures = cultureList.OrderBy(x => x.DisplayName).ToList();
        TranslationNames = KnownCultures
            .Select(x => $"{x.DisplayName} [{x.Name}]")
            .ToList();
        TranslationCountryCodes = KnownCultures
            .Select(x => x.Name)
            .ToList();

        void AliasLocale(string original, string alias)
        {
            var locale = cultureList.Find(x => x.Name == original);
        
            if (locale != null)
                cultureList.Add(new SimpleCultureInfo(locale.DisplayName, alias));
        }
    }
    
    // TODO: Better pattern?
    public MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public static List<SimpleCultureInfo> KnownCultures { get; }

    public static List<string> TranslationNames { get; }
    
    public static List<string> TranslationCountryCodes { get; }

    public int SelectedCountryCode { get; set; } = -1;
    
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