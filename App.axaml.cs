using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using MegaDatos.Services;
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
            var savedState = AppStateService.Instance.LoadState();
            var vm = new MainViewModel();

            var mainWindow = new MainWindow
            {
                DataContext = vm,
            };

            mainWindow.ApplySavedWindowState(savedState);

            vm.RequestOpenFolderAsync = async () =>
            {
                var result = await mainWindow.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Seleccionar Carpeta",
                    AllowMultiple = false
                });
                return result.Count > 0 ? result[0].TryGetLocalPath() : null;
            };

            vm.RequestSaveKmzAsync = async () =>
            {
                var result = await mainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Guardar archivo KMZ",
                    DefaultExtension = "kmz",
                    SuggestedFileName = "Coordenadas.kmz",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Archivos KMZ (*.kmz)") { Patterns = new[] { "*.kmz" } }
                    }
                });
                return result?.TryGetLocalPath();
            };

            desktop.MainWindow = mainWindow;
            
            // Cargar estado previo en el ViewModel
            vm.LoadSavedState(savedState);

            Console.WriteLine("App: MainWindow instantiated and state loaded");
        }
        else
        {
            Console.WriteLine("App: ApplicationLifetime is NOT desktop!");
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("App: OnFrameworkInitializationCompleted completed");
    }
}