using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MegaDatos.Models;

namespace MegaDatos.Services;

public class AppStateService
{
    private static readonly Lazy<AppStateService> _instance = new(() => new AppStateService());
    public static AppStateService Instance => _instance.Value;

    private readonly string _filePath;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AppStateService()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "MegaDatos");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _filePath = Path.Combine(appFolder, "state.json");
        }
        catch
        {
            // Fallback to local directory if AppData is not accessible
            _filePath = "state.json";
        }
    }

    public AppState LoadState()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                string json = File.ReadAllText(_filePath);
                var state = JsonSerializer.Deserialize<AppState>(json, _jsonOptions);
                if (state != null)
                {
                    return state;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppStateService] Error al cargar estado: {ex.Message}");
        }

        return new AppState();
    }

    public void SaveState(AppState state)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(state, _jsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppStateService] Error al guardar estado: {ex.Message}");
        }
    }

    public async Task SaveStateAsync(AppState state)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppStateService] Error al guardar estado de forma asíncrona: {ex.Message}");
        }
    }
}
