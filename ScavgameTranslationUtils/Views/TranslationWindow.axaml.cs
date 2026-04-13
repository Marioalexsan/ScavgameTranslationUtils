using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ScavgameTranslationUtils.ViewModels;

namespace ScavgameTranslationUtils.Views;

// TODO: Refactor this junk file
public partial class TranslationWindow : Window
{
    // TODO: Better pattern?
    public TranslationWindowViewModel? ViewModel => DataContext as TranslationWindowViewModel;

    private Task _currentSaveTask = Task.CompletedTask;
    private bool _shouldSaveOnClose = true;

    public bool AutosaveEnabled { get; set; } = true;

    public TranslationWindow()
    {
        InitializeComponent();
    }

    private async void SaveChangesManual(object? sender, RoutedEventArgs e)
    {
        await SaveChanges(true);
    }

    private async void AutosaveChanges(object? sender, RoutedEventArgs e)
    {
        if (!AutosaveEnabled)
            return;
        
        await SaveChanges();
    }

    private async void WindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_shouldSaveOnClose)
            await SaveChanges(true);
    }

    private async Task SaveChanges(bool force = false)
    {
        if (ViewModel == null)
            return;

        var workspace = ViewModel.Workspace;

        await _currentSaveTask; // If there's anything pending, wait for it first
        _currentSaveTask = Task.Run(() => workspace.SaveChanges(force));
        await _currentSaveTask;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        _shouldSaveOnClose = true;
        Close();
    }

    private void CloseWindowWithoutSaving(object? sender, RoutedEventArgs e)
    {
        _shouldSaveOnClose = false;
        Close();
    }
}