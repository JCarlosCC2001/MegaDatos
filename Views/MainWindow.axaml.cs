using System;
using Avalonia.Controls;
using MegaDatos.Models;
using MegaDatos.Services;
using MegaDatos.ViewModels;

namespace MegaDatos.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        Console.WriteLine("MainWindow: Constructor started");
        InitializeComponent();
        Console.WriteLine("MainWindow: InitializeComponent completed");

        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        try
        {
            if (DataContext is MainViewModel vm)
            {
                var state = vm.GetCurrentState();
                state.WindowState = WindowState.ToString();
                if (WindowState == WindowState.Normal)
                {
                    state.WindowWidth = Width;
                    state.WindowHeight = Height;
                    state.WindowPositionX = Position.X;
                    state.WindowPositionY = Position.Y;
                }
                AppStateService.Instance.SaveState(state);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainWindow] Error guardando estado al cerrar: {ex.Message}");
        }
    }

    public void ApplySavedWindowState(AppState state)
    {
        try
        {
            if (state.WindowWidth.HasValue && state.WindowWidth.Value >= 800)
            {
                Width = state.WindowWidth.Value;
            }
            if (state.WindowHeight.HasValue && state.WindowHeight.Value >= 500)
            {
                Height = state.WindowHeight.Value;
            }
            if (state.WindowPositionX.HasValue && state.WindowPositionY.HasValue)
            {
                Position = new Avalonia.PixelPoint(state.WindowPositionX.Value, state.WindowPositionY.Value);
            }
            if (!string.IsNullOrEmpty(state.WindowState) && Enum.TryParse<WindowState>(state.WindowState, out var ws))
            {
                WindowState = ws;
            }
        }
        catch { }
    }
}