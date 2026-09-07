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
            var splashWindow = new SplashWindow();
            desktop.MainWindow = splashWindow;

            // Iniciar inicialización pesada en segundo plano
            _ = Task.Run(async () =>
            {
                // Simulamos un pequeño tiempo extra si la carga es muy rápida para que el usuario pueda ver el logo
                var minimumSplashTime = Task.Delay(1500);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => splashWindow.UpdateStatus("Cargando estado guardado..."));
                var savedState = AppStateService.Instance.LoadState();
                
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => splashWindow.UpdateStatus("Inicializando base de datos local..."));
                var vm = new MainViewModel();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => splashWindow.UpdateStatus("Restaurando sesión anterior..."));
                vm.LoadSavedState(savedState);

                // Esperar al tiempo mínimo de splash
                await minimumSplashTime;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
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

                    // Reemplazar la ventana principal del ciclo de vida
                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    
                    // Cerrar el splash
                    splashWindow.Close();
                });
            });

            Console.WriteLine("App: SplashWindow instantiated and loading background tasks");
        }
        else
        {
            Console.WriteLine("App: ApplicationLifetime is NOT desktop!");
        }

        base.OnFrameworkInitializationCompleted();
        Console.WriteLine("App: OnFrameworkInitializationCompleted completed");
    }
}