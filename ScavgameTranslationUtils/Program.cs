using Avalonia;
using System;
using System.IO;
using System.Runtime.InteropServices;
using ScavgameTranslationUtils.Models;

namespace ScavgameTranslationUtils;

static class Program
{
    // Store in the same folder as the game data to keep it simple
    // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-persistentDataPath.html
    public static string AppDataPath
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Path.Combine(
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}Low",
                    "Orsoniks",
                    "CasualtiesUnknown",
                    "ScavgameTranslationUtils");
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home =  Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                
                return Path.Combine(
                    home,
                    "unity3d",
                    "Orsoniks",
                    "CasualtiesUnknown",
                    "ScavgameTranslationUtils");
            }
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // TODO: uses the Editor path instead of the Player path if that directory already exists
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                return Path.Combine(
                    home,
                    "Library",
                    "Application Support",
                    $"unity.Orsoniks.CasualtiesUnknown",
                    "ScavgameTranslationUtils");
            }
            
            // Ehh, screw it
            return Directory.GetCurrentDirectory();
        }
    }
    
    public static AppSettings Settings { get; private set; }

    static Program()
    {
        Settings = AppSettings.LoadAsync().Result;
    }
    
    [STAThread]
    public static void Main(string[] args) // do not change this to async
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
