using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MegaDatos.ViewModels;
using MegaDatos.Views;

namespace MegaDatos;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Console.WriteLine("App: OnFrameworkInitializationCompleted started");
        Console.WriteLine($"App: Lifetime type is {ApplicationLifetime?.GetType().FullName}");
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Console.WriteLine("App: ApplicationLifetime is IClassicDesktopStyleApplicationLifetime");
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(),
            };
            Console.WriteLine("App: MainWindow instantiated and set");
        }
        else
        {
            Console.WriteLine("App: ApplicationLifetime is NOT desktop!");
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("App: OnFrameworkInitializationCompleted completed");
    }
}