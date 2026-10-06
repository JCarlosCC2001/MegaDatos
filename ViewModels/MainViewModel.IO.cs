using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDatos.Models;
using MegaDatos.Models.Devices;
using MegaDatos.Models.Templates;
using MegaDatos.Services;
using ImageMagick;

namespace MegaDatos.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // ===== Safe File System Helpers =====
    private IEnumerable<string> SafeGetDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    private IEnumerable<FileInfo> SafeGetFiles(string path)
    {
        try
        {
            var di = new DirectoryInfo(path);
            return di.EnumerateFiles();
        }
        catch
        {
            return Enumerable.Empty<FileInfo>();
        }
    }

    // ===== Real FileSystem Data =====
    public async void LoadRealData(string path, string? targetFileToSelect = null)
    {
        RootDirectory = path;
        CurrentPath = path;
        _pendingSelectedFile = targetFileToSelect;
        StatusText = $"Cargando {path}...";
        HasDirectoryLoaded = false;
        
        // Clear UI early for visual feedback
        Files = new ObservableCollection<FileItem>();
        FolderTree = new ObservableCollection<FolderNode>();

        try
        {
            await Task.Run(() =>
            {
                // 1. Load Tree (Background)
                var rootNode = new FolderNode($"📁 {Path.GetFileName(path) ?? path}", path);
                rootNode.IsExpanded = true;

                Dispatcher.UIThread.InvokeAsync(() => 
                {
                    FolderTree = new ObservableCollection<FolderNode> { rootNode };
                    // Autoselect root node
                    SelectedFolderNode = rootNode;
                });
            });

            HasDirectoryLoaded = true;
            CalculateGpsStatistics();
            SaveCurrentState();
        }
        catch (Exception ex)
        {
            StatusText = $"Error crítico: {ex.Message}";
            HasDirectoryLoaded = false;
        }
    }

    private FileItem CreateFileItem(FileInfo fi)
    {
        string cat = GetCategoryForExtension(fi.Extension);
        string res = "—";
        string gpsDec = "—";
        string gpsDMS = "—";

        if (cat == "Image")
        {
            try
            {
                var info = new MagickImageInfo(fi.FullName);
                res = $"{info.Width} × {info.Height} px";
                
                using var img = new MagickImage(fi.FullName);
                var profile = img.GetExifProfile();
                if (profile != null)
                {
                    var latRefVal = profile.GetValue(ExifTag.GPSLatitudeRef);
                    var latVal = profile.GetValue(ExifTag.GPSLatitude);
                    var lonRefVal = profile.GetValue(ExifTag.GPSLongitudeRef);
                    var lonVal = profile.GetValue(ExifTag.GPSLongitude);

                    if (latVal != null && lonVal != null && latRefVal != null && lonRefVal != null)
                    {
                        var lat = latVal.Value as Rational[];
                        var lon = lonVal.Value as Rational[];
                        var latRef = latRefVal.Value as string;
                        var lonRef = lonRefVal.Value as string;

                        if (lat != null && lat.Length == 3 && lon != null && lon.Length == 3 && latRef != null && lonRef != null)
                        {
                            double latD = (double)lat[0].Numerator / lat[0].Denominator;
                            double latM = (double)lat[1].Numerator / lat[1].Denominator;
                            double latS = (double)lat[2].Numerator / lat[2].Denominator;
                            double latDec = latD + (latM / 60.0) + (latS / 3600.0);
                            if (latRef.Equals("S", StringComparison.OrdinalIgnoreCase)) latDec = -latDec;

                            double lonD = (double)lon[0].Numerator / lon[0].Denominator;
                            double lonM = (double)lon[1].Numerator / lon[1].Denominator;
                            double lonS = (double)lon[2].Numerator / lon[2].Denominator;
                            double lonDec = lonD + (lonM / 60.0) + (lonS / 3600.0);
                            if (lonRef.Equals("W", StringComparison.OrdinalIgnoreCase)) lonDec = -lonDec;

                            gpsDec = $"{latDec.ToString("F6", CultureInfo.InvariantCulture)}, {lonDec.ToString("F6", CultureInfo.InvariantCulture)}";
                            
                            string latDir = latDec >= 0 ? "N" : "S";
                            string lonDir = lonDec >= 0 ? "E" : "W";
                            gpsDMS = $"{latD}° {latM}' {latS.ToString("F2", CultureInfo.InvariantCulture)}\" {latDir}, {lonD}° {lonM}' {lonS.ToString("F2", CultureInfo.InvariantCulture)}\" {lonDir}";
                        }
                    }
                }
            }
            catch { }
        }

        return new FileItem
        {
            Name = fi.Name,
            FullPath = fi.FullName,
            Extension = fi.Extension,
            SizeBytes = fi.Length,
            DateModified = fi.LastWriteTime,
            FileType = string.IsNullOrEmpty(fi.Extension) ? "Archivo" : fi.Extension.TrimStart('.').ToUpperInvariant(),
            Resolution = res,
            GpsDecimal = gpsDec,
            GpsDMS = gpsDMS
        };
    }

    private async Task ReloadFilesFromPathAsync(string path, string? targetFileToSelect = null)
    {
        IsProcessing = true;
        var filesList = new ObservableCollection<FileItem>();
        
        await Task.Run(() =>
        {
            var fileInfos = SafeGetFiles(path).ToList();
            int count = 0;

            foreach (var fi in fileInfos)
            {
                var item = CreateFileItem(fi);

                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(FileItem.IsSelected))
                    {
                        SelectedCount = Files.Count(f => f.IsSelected);
                        UpdateSupportedFormats();
                        if (!_isUpdatingSelectAll)
                        {
                            UpdateAreAllFilesSelectedState();
                        }
                    }
                };

                filesList.Add(item);
                count++;

                // Enviar a la UI en lotes de 500 para evitar congelamientos en directorios masivos
                if (count % 500 == 0)
                {
                    var partialList = new ObservableCollection<FileItem>(filesList);
                    Dispatcher.UIThread.InvokeAsync(() => 
                    {
                        Files = partialList;
                        TotalFiles = Files.Count;
                        UpdateAreAllFilesSelectedState();
                        StatusText = $"Cargando... {count} archivos procesados.";
                    });
                }
            }
        });

        // Final commit to UI
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Files = filesList;
            TotalFiles = Files.Count;
            SelectedCount = 0;
            UpdateAreAllFilesSelectedState();
            if (Files.Count > 0)
            {
                FileItem? fileToSelect = null;
                if (!string.IsNullOrEmpty(targetFileToSelect))
                {
                    fileToSelect = filesList.FirstOrDefault(f => string.Equals(f.FullPath, targetFileToSelect, StringComparison.OrdinalIgnoreCase));
                }

                fileToSelect ??= filesList[0];

                SelectFile(fileToSelect);
                UpdateSupportedFormats();
            }
            else
            {
                SelectedFile = null;
                HasFileSelected = false;
                SelectedFileName = string.Empty;
                SupportedFormats.Clear();
            }
            StatusText = $"Directorio cargado: {Files.Count} archivos";
            EvaluateAllFilesCompliance();
            CalculateGpsStatistics();
            SaveCurrentState();
            IsProcessing = false;
        });
    }

    private void SetupWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemChanged;
        _watcher.Changed += OnFileSystemChanged;
    }

    private DateTime _lastReload = DateTime.MinValue;

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        // Ignorar eventos fuera del directorio actual
        string? dir = Path.GetDirectoryName(e.FullPath);
        if (!string.Equals(dir, CurrentPath, StringComparison.OrdinalIgnoreCase)) return;

        Dispatcher.UIThread.InvokeAsync(() => 
        {
            if (Files == null) return;
            
            if (e.ChangeType == WatcherChangeTypes.Created || e.ChangeType == WatcherChangeTypes.Renamed)
            {
                // Si fue renombrado, remover el viejo primero (e es RenamedEventArgs)
                if (e is RenamedEventArgs renamedEvent)
                {
                    var oldItem = Files.FirstOrDefault(f => string.Equals(f.FullPath, renamedEvent.OldFullPath, StringComparison.OrdinalIgnoreCase));
                    if (oldItem != null) Files.Remove(oldItem);
                }
                
                // Agregar el nuevo archivo
                if (File.Exists(e.FullPath))
                {
                    // Evitar duplicados
                    if (!Files.Any(f => string.Equals(f.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        var fi = new FileInfo(e.FullPath);
                        if (!fi.Attributes.HasFlag(FileAttributes.Directory))
                        {
                            var newItem = CreateFileItem(fi);
                            Files.Add(newItem);
                            TotalFiles = Files.Count;
                            StatusText = $"Directorio actualizado: {TotalFiles} archivos";
                            UpdateAreAllFilesSelectedState();
                        }
                    }
                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Deleted)
            {
                var itemToRemove = Files.FirstOrDefault(f => string.Equals(f.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase));
                if (itemToRemove != null)
                {
                    Files.Remove(itemToRemove);
                    TotalFiles = Files.Count;
                    StatusText = $"Directorio actualizado: {TotalFiles} archivos";
                    UpdateAreAllFilesSelectedState();
                }
            }
            else if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                var existingItem = Files.FirstOrDefault(f => string.Equals(f.FullPath, e.FullPath, StringComparison.OrdinalIgnoreCase));
                if (existingItem != null && File.Exists(e.FullPath))
                {
                    // Actualizar en el lugar en vez de recrear
                    var fi = new FileInfo(e.FullPath);
                    existingItem.SizeBytes = fi.Length;
                    existingItem.DateModified = fi.LastWriteTime;
                    // Los metadatos profundos (Exif/GPS) no los recargamos aquí para evitar freeze, 
                    // a menos que sea seleccionado y cargado en LoadRealData.
                }
            }
        });
    }


    [ObservableProperty]
    private string _selectedGpsFormat = "Decimal";

    partial void OnSelectedGpsFormatChanged(string value)
    {
        ConvertGpsDataFormat(value);
        SaveCurrentState();
    }

    private void ConvertGpsDataFormat(string targetFormat)
    {
        if (GpsData == null) return;
        foreach (var entry in GpsData)
        {
            if (entry.IsGpsCoordinate)
            {
                entry.GpsFormat = targetFormat;
            }
        }
    }

    [ObservableProperty]
    private string _selectedViewType = "Por defecto";

    [ObservableProperty]
    private string _gpsRefLat = "—";
    partial void OnGpsRefLatChanged(string value) => CalculateGpsStatistics(true);

    [ObservableProperty]
    private string _gpsRefLon = "—";
    partial void OnGpsRefLonChanged(string value) => CalculateGpsStatistics(true);

    [ObservableProperty]
    private string _gpsMinDistance = "—";

    [ObservableProperty]
    private string _gpsMaxDistance = "—";

    [ObservableProperty]
    private string _gpsAverageDistance = "—";

    public ObservableCollection<string> ViewTypes { get; } = new()
    {
        "Por defecto",
        "Imágenes",
        "Coordenadas GPS",
        "Verificación de Plantilla"
    };

    partial void OnSelectedViewTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsResolutionColumnVisible));
        OnPropertyChanged(nameof(IsMegapixelColumnVisible));
        OnPropertyChanged(nameof(IsGpsColumnVisible));
        OnPropertyChanged(nameof(IsGpsBannerVisible));
        OnPropertyChanged(nameof(IsTemplateColumnVisible));
        OnPropertyChanged(nameof(IsTemplateDetailsColumnVisible));
        OnPropertyChanged(nameof(IsTemplateBannerVisible));
        OnPropertyChanged(nameof(IsTypeColumnVisible));
        OnPropertyChanged(nameof(IsSizeColumnVisible));
        OnPropertyChanged(nameof(IsDateColumnVisible));
        SaveCurrentState();

        if (value == "Verificación de Plantilla" || value == "Plantillas / Alertas")
        {
            EvaluateAllFilesCompliance();
        }
    }

    public bool IsResolutionColumnVisible => SelectedViewType == "Imágenes";
    public bool IsMegapixelColumnVisible => SelectedViewType == "Imágenes";
    public bool IsGpsColumnVisible => SelectedViewType == "Coordenadas GPS" || SelectedViewType == "Coordenadas";
    public bool IsGpsBannerVisible => SelectedViewType == "Coordenadas GPS" || SelectedViewType == "Coordenadas";
    public bool IsTemplateColumnVisible => SelectedViewType == "Verificación de Plantilla" || SelectedViewType == "Plantillas / Alertas";
    public bool IsTemplateDetailsColumnVisible => SelectedViewType == "Verificación de Plantilla" || SelectedViewType == "Plantillas / Alertas";
    public bool IsTemplateBannerVisible => SelectedViewType == "Verificación de Plantilla" || SelectedViewType == "Plantillas / Alertas";
    public bool IsTypeColumnVisible => SelectedViewType == "Por defecto" || SelectedViewType == "Imágenes";
    public bool IsSizeColumnVisible => SelectedViewType == "Por defecto";
    public bool IsDateColumnVisible => SelectedViewType == "Por defecto";

    // ===== State Persistence =====
    public AppState GetCurrentState()
    {
        return new AppState
        {
            LastDirectory = !string.IsNullOrEmpty(RootDirectory) && Directory.Exists(RootDirectory) ? RootDirectory : null,
            LastSelectedFile = SelectedFile?.FullPath,
            ActiveTool = ActiveTool,
            SelectedViewType = SelectedViewType,
            SelectedGpsFormat = SelectedGpsFormat,
            TargetFormat = TargetFormat,
            ConversionQuality = ConversionQuality,
            TargetWidth = TargetWidth,
            TargetHeight = TargetHeight,
            KeepAspectRatio = KeepAspectRatio,
            CleanAllMetadata = CleanAllMetadata,
            CleanGps = CleanGps,
            CleanExif = CleanExif,
            CleanAuthorship = CleanAuthorship,
            CleanIptc = CleanIptc,
            BatchBackupEnabled = BatchBackupEnabled,
            SelectedTemplateId = SelectedTemplate?.Id ?? "Photoshop",
            SelectedPhoneDeviceId = SelectedPhoneDevice?.Id ?? "iphone15pro",
            PhoneOverwriteAll = PhoneOverwriteAll,
            PhonePreserveGps = PhonePreserveGps,
            GpsGenCenterLat = GpsGenCenterLat,
            GpsGenCenterLon = GpsGenCenterLon,
            GpsGenRadius = GpsGenRadius,
            GpsGenRadiusUnit = GpsGenRadiusUnit,
            GpsGenUniquePerPhoto = GpsGenUniquePerPhoto,
            GpsGenBaseAltitude = GpsGenBaseAltitude
        };
    }

    public void SaveCurrentState()
    {
        if (_isRestoringState) return;

        try
        {
            AppStateService.Instance.SaveState(GetCurrentState());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] Error al guardar estado: {ex.Message}");
        }
    }

    public void LoadSavedState(AppState? state = null)
    {
        try
        {
            _isRestoringState = true;
            state ??= AppStateService.Instance.LoadState();
            if (state != null)
            {
                if (!string.IsNullOrEmpty(state.ActiveTool)) ActiveTool = state.ActiveTool;
                if (!string.IsNullOrEmpty(state.SelectedViewType)) SelectedViewType = state.SelectedViewType;
                if (!string.IsNullOrEmpty(state.SelectedGpsFormat)) SelectedGpsFormat = state.SelectedGpsFormat;
                if (!string.IsNullOrEmpty(state.TargetFormat)) TargetFormat = state.TargetFormat;
                if (state.ConversionQuality > 0) ConversionQuality = state.ConversionQuality;
                if (!string.IsNullOrEmpty(state.TargetWidth)) TargetWidth = state.TargetWidth;
                if (!string.IsNullOrEmpty(state.TargetHeight)) TargetHeight = state.TargetHeight;
                KeepAspectRatio = state.KeepAspectRatio;
                CleanAllMetadata = state.CleanAllMetadata;
                CleanGps = state.CleanGps;
                CleanExif = state.CleanExif;
                CleanAuthorship = state.CleanAuthorship;
                CleanIptc = state.CleanIptc;
                BatchBackupEnabled = state.BatchBackupEnabled;
                PhoneOverwriteAll = state.PhoneOverwriteAll;
                PhonePreserveGps = state.PhonePreserveGps;

                if (!string.IsNullOrEmpty(state.GpsGenCenterLat)) GpsGenCenterLat = state.GpsGenCenterLat;
                if (!string.IsNullOrEmpty(state.GpsGenCenterLon)) GpsGenCenterLon = state.GpsGenCenterLon;
                if (!string.IsNullOrEmpty(state.GpsGenRadius)) GpsGenRadius = state.GpsGenRadius;
                if (!string.IsNullOrEmpty(state.GpsGenRadiusUnit)) GpsGenRadiusUnit = state.GpsGenRadiusUnit;
                GpsGenUniquePerPhoto = state.GpsGenUniquePerPhoto;
                if (!string.IsNullOrEmpty(state.GpsGenBaseAltitude)) GpsGenBaseAltitude = state.GpsGenBaseAltitude;

                if (!string.IsNullOrEmpty(state.SelectedTemplateId))
                {
                    var found = AvailableTemplates.FirstOrDefault(t => string.Equals(t.Id, state.SelectedTemplateId, StringComparison.OrdinalIgnoreCase));
                    if (found != null) SelectedTemplate = found;
                }

                if (!string.IsNullOrEmpty(state.SelectedPhoneDeviceId))
                {
                    var foundPhone = MegaDatos.Models.Devices.PhoneDeviceRegistry.Instance.Devices.FirstOrDefault(d => string.Equals(d.Id, state.SelectedPhoneDeviceId, StringComparison.OrdinalIgnoreCase));
                    if (foundPhone != null)
                    {
                        SelectedPhoneBrand = foundPhone.Brand;
                        SelectedPhoneDevice = foundPhone;
                    }
                }

                if (!string.IsNullOrEmpty(state.LastDirectory) && Directory.Exists(state.LastDirectory))
                {
                    LoadRealData(state.LastDirectory, state.LastSelectedFile);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MainViewModel] Error al restaurar estado: {ex.Message}");
        }
        finally
        {
            _isRestoringState = false;
        }
    }
}
