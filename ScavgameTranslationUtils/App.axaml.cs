using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ScavgameTranslationUtils.ViewModels;
using ScavgameTranslationUtils.Views;

namespace ScavgameTranslationUtils;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MainWindowViewModel viewModel;
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel = new MainWindowViewModel(),
            };
            
            // TODO: Hacky?
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (File.Exists(Program.Settings.GameExecutablePath))
                    await viewModel.OpenGameAssets(Program.Settings.GameExecutablePath);
        
                if (File.Exists(Program.Settings.EnglishTranslationPath))
                    await viewModel.OpenOriginalTranslation(Program.Settings.EnglishTranslationPath);
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}