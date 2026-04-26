using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    private DispatcherTimer _periodicBackupTimer;

    public TranslationWindow()
    {
        InitializeComponent();
        Program.LogDebug("Starting backup timer.");
        _periodicBackupTimer = new DispatcherTimer(TimeSpan.FromMinutes(5), DispatcherPriority.Background, OnPeriodicBackupTimerTick);
    }

    protected override void OnClosed(EventArgs e)
    {
        Program.LogDebug("Stopping backup timer.");
        _periodicBackupTimer.Stop();
    }

    private void OnPeriodicBackupTimerTick(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            if (ViewModel == null)
                return;

            var workspace = ViewModel.Workspace;

            await _currentSaveTask; // If there's anything pending, wait for it first
            _currentSaveTask = Task.Run(() => workspace.SaveBackup(periodic: true));
            await _currentSaveTask;
        });
    }

    private async void SaveChangesManual(object? sender, RoutedEventArgs e)
    {
        await SaveChanges(true);
    }

    private async void AutosaveChanges(object? sender, RoutedEventArgs e)
    {
        if (ViewModel == null || !ViewModel.AutosaveEnabled)
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