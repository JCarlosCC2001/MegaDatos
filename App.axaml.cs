using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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
        
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = vm,
            };

            vm.RequestOpenFolderAsync = async () =>
            {
                var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Seleccionar Carpeta",
                    AllowMultiple = false
                });
                return result.Count > 0 ? result[0].TryGetLocalPath() : null;
            };

            desktop.MainWindow = mainWindow;
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