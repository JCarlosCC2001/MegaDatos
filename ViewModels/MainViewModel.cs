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
    // ===== Sidebar State =====
    private bool _isRestoringState = false;
    private string? _pendingSelectedFile;

    [ObservableProperty]
    private string _activeTool = "Explorer";

    public bool IsExplorerActive => ActiveTool == "Explorer";
    public bool IsGpsGenActive => ActiveTool == "GpsGen";
    public bool IsPhoneActive => ActiveTool == "Phone";
    public bool IsTemplatesActive => ActiveTool == "Templates";
    public bool IsCleanActive => ActiveTool == "Clean";
    public bool IsFormatActive => ActiveTool == "Format";
    public bool IsResizeActive => ActiveTool == "Resize";
    public bool IsKmzExportActive => ActiveTool == "KmzExport";

    partial void OnActiveToolChanged(string value)
    {
        OnPropertyChanged(nameof(IsExplorerActive));
        OnPropertyChanged(nameof(IsGpsGenActive));
        OnPropertyChanged(nameof(IsPhoneActive));
        OnPropertyChanged(nameof(IsTemplatesActive));
        OnPropertyChanged(nameof(IsCleanActive));
        OnPropertyChanged(nameof(IsFormatActive));
        OnPropertyChanged(nameof(IsResizeActive));
        OnPropertyChanged(nameof(IsKmzExportActive));
        StatusText = $"Herramienta activa: {value}";
        SaveCurrentState();

        if (value == "Templates")
        {
            SelectedViewType = "Verificación de Plantilla";
            EvaluateAllFilesCompliance();
        }
        else if (value == "GpsGen")
        {
            SelectedViewType = "Coordenadas GPS";
        }
    }

    [ObservableProperty]
    private string _currentPath = string.Empty;
    public string RootDirectory { get; private set; } = string.Empty;

    // ===== File Explorer =====
    [ObservableProperty]
    private ObservableCollection<FolderNode> _folderTree = new();

    [ObservableProperty]
    private ObservableCollection<FileItem> _files = new();

    private bool _isUpdatingSelectAll = false;
    private bool? _areAllFilesSelected = false;
    public bool? AreAllFilesSelected
    {
        get => _areAllFilesSelected;
        set
        {
            // When user clicks the checkbox and current state is null (indeterminate),
            // force it to true (select all)
            if (!value.HasValue) value = true;
            
            if (_areAllFilesSelected != value)
            {
                _areAllFilesSelected = value;
                OnPropertyChanged(nameof(AreAllFilesSelected));

                if (!_isUpdatingSelectAll && Files != null)
                {
                    _isUpdatingSelectAll = true;
                    bool select = value.Value;
                    foreach (var file in Files)
                    {
                        if (file.IsCheckable)
                        {
                            file.IsSelected = select;
                        }
                    }
                    SelectedCount = Files.Count(f => f.IsSelected);
                    UpdateSupportedFormats();
                    _isUpdatingSelectAll = false;
                }
            }
        }
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        if (Files == null || Files.Count == 0) return;

        bool anyUnselected = Files.Any(f => f.IsCheckable && !f.IsSelected);
        bool targetState = anyUnselected;

        _isUpdatingSelectAll = true;
        foreach (var file in Files)
        {
            if (file.IsCheckable)
            {
                file.IsSelected = targetState;
            }
        }
        SelectedCount = Files.Count(f => f.IsSelected);
        UpdateSupportedFormats();
        _isUpdatingSelectAll = false;
        UpdateAreAllFilesSelectedState();
    }

    public void UpdateAreAllFilesSelectedState()
    {
        if (Files == null || Files.Count == 0)
        {
            _areAllFilesSelected = false;
            OnPropertyChanged(nameof(AreAllFilesSelected));
            return;
        }

        var checkable = Files.Where(f => f.IsCheckable).ToList();
        if (checkable.Count == 0)
        {
            _areAllFilesSelected = false;
        }
        else if (checkable.All(f => f.IsSelected))
        {
            _areAllFilesSelected = true;
        }
        else if (checkable.All(f => !f.IsSelected))
        {
            _areAllFilesSelected = false;
        }
        else
        {
            _areAllFilesSelected = null;
        }
        OnPropertyChanged(nameof(AreAllFilesSelected));
    }

    [ObservableProperty]
    private FileItem? _selectedFile;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _selectedCount;

    // ===== Metadata Inspector =====
    [ObservableProperty]
    private ObservableCollection<MetadataEntry> _generalInfo = new();

    [ObservableProperty]
    private ObservableCollection<MetadataEntry> _exifData = new();

    [ObservableProperty]
    private ObservableCollection<MetadataEntry> _authorshipData = new();

    [ObservableProperty]
    private ObservableCollection<MetadataEntry> _gpsData = new();

    [ObservableProperty]
    private ObservableCollection<MetadataEntry> _iptcData = new();

    // ===== Metadata Editor Fields =====
    [ObservableProperty]
    private string _editAuthor = string.Empty;

    [ObservableProperty]
    private string _editCopyright = string.Empty;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private string _editSoftware = string.Empty;

    [ObservableProperty]
    private string _editTags = string.Empty;

    // ===== GPS Editor =====
    [ObservableProperty]
    private string _gpsLatitude = string.Empty;

    [ObservableProperty]
    private string _gpsLongitude = string.Empty;

    [ObservableProperty]
    private string _gpsAltitude = string.Empty;

    // ===== Clean Metadata Tool =====
    [ObservableProperty]
    private bool _cleanAllMetadata = true;

    partial void OnCleanAllMetadataChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _cleanGps = true;

    partial void OnCleanGpsChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _cleanExif = true;

    partial void OnCleanExifChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _cleanAuthorship = true;

    partial void OnCleanAuthorshipChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _cleanIptc = true;

    partial void OnCleanIptcChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _batchBackupEnabled = true;

    partial void OnBatchBackupEnabledChanged(bool value) => SaveCurrentState();

    // ===== Metadata Templates Tool =====
    public ObservableCollection<IMetadataTemplate> AvailableTemplates { get; } = new();

    [ObservableProperty]
    private IMetadataTemplate? _selectedTemplate;

    [ObservableProperty]
    private int _compliantCount;

    [ObservableProperty]
    private int _nonCompliantCount;

    [ObservableProperty]
    private int _incompleteCount;

    public bool IsTemplateApplyEnabled => SelectedTemplate != null && SelectedTemplate.Id != "None";
    public bool IsTemplateActiveSelected => SelectedTemplate != null && SelectedTemplate.Id != "None";

    partial void OnSelectedTemplateChanged(IMetadataTemplate? value)
    {
        OnPropertyChanged(nameof(IsTemplateApplyEnabled));
        OnPropertyChanged(nameof(IsTemplateActiveSelected));
        SaveCurrentState();
        EvaluateAllFilesCompliance();
    }

    // ===== Phone Device Profiles Tool =====
    public ObservableCollection<PhoneDeviceProfile> AvailablePhoneDevices { get; } = new();

    [ObservableProperty]
    private PhoneDeviceProfile? _selectedPhoneDevice;

    partial void OnSelectedPhoneDeviceChanged(PhoneDeviceProfile? value) => SaveCurrentState();

    [ObservableProperty]
    private bool _phoneOverwriteAll = false;

    partial void OnPhoneOverwriteAllChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _phonePreserveGps = true;

    partial void OnPhonePreserveGpsChanged(bool value) => SaveCurrentState();

    // ===== GPS Generator Tool =====
    [ObservableProperty]
    private string _gpsGenCenterLat = "-12.065130";

    partial void OnGpsGenCenterLatChanged(string value) => SaveCurrentState();

    [ObservableProperty]
    private string _gpsGenCenterLon = "-75.204860";

    partial void OnGpsGenCenterLonChanged(string value) => SaveCurrentState();

    [ObservableProperty]
    private string _gpsGenRadius = "500";

    partial void OnGpsGenRadiusChanged(string value) => SaveCurrentState();

    [ObservableProperty]
    private string _gpsGenRadiusUnit = "Metros (m)";

    partial void OnGpsGenRadiusUnitChanged(string value) => SaveCurrentState();

    public ObservableCollection<string> GpsGenRadiusUnits { get; } = new()
    {
        "Metros (m)",
        "Kilómetros (km)"
    };

    [ObservableProperty]
    private bool _gpsGenUniquePerPhoto = true;

    partial void OnGpsGenUniquePerPhotoChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private string _gpsGenBaseAltitude = "3250";

    partial void OnGpsGenBaseAltitudeChanged(string value) => SaveCurrentState();

    // ===== KMZ Export Tool =====
    public ObservableCollection<string> AvailableKmzColors { get; } = new()
    {
        "Rojo", "Azul", "Verde", "Amarillo", "Blanco"
    };

    [ObservableProperty]
    private string _kmzMarkerColor = "Rojo";

    [ObservableProperty]
    private bool _kmzIncludePath = false;

    [ObservableProperty]
    private bool _kmzIncludeDate = true;

    [ObservableProperty]
    private bool _kmzIncludeFilename = true;

    partial void OnKmzMarkerColorChanged(string value) => SaveCurrentState();
    partial void OnKmzIncludePathChanged(bool value) => SaveCurrentState();
    partial void OnKmzIncludeDateChanged(bool value) => SaveCurrentState();
    partial void OnKmzIncludeFilenameChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private bool _kmzEnableComparison = false;
    partial void OnKmzEnableComparisonChanged(bool value) { SaveCurrentState(); KmzStatsVisible = false; }


    [ObservableProperty]
    private bool _kmzIncludeRefPoint = true;
    partial void OnKmzIncludeRefPointChanged(bool value) => SaveCurrentState();

    public ObservableCollection<string> AvailableKmzRefSymbols { get; } = new()
    {
        "Estrella", "Círculo", "Chincheta", "Cuadrado", "Triángulo"
    };

    [ObservableProperty]
    private string _kmzRefSymbol = "Estrella";
    partial void OnKmzRefSymbolChanged(string value) => SaveCurrentState();

    [ObservableProperty]
    private bool _kmzDrawCircle = true;
    partial void OnKmzDrawCircleChanged(bool value) => SaveCurrentState();

    [ObservableProperty]
    private string _kmzCircleRadius = "1000";
    partial void OnKmzCircleRadiusChanged(string value) => SaveCurrentState();

    [ObservableProperty]
    private bool _kmzStatsVisible = false;

    [ObservableProperty]
    private string _kmzMinDist = string.Empty;

    [ObservableProperty]
    private string _kmzMaxDist = string.Empty;

    [ObservableProperty]
    private string _kmzAvgDist = string.Empty;

    [ObservableProperty]
    private string _gpsGenPreviewLat = string.Empty;

    [ObservableProperty]
    private string _gpsGenPreviewLon = string.Empty;

    [ObservableProperty]
    private string _gpsGenPreviewDms = string.Empty;

    [ObservableProperty]
    private bool _hasGpsGenPreview = false;

    [RelayCommand]
    private void GpsGenSetRadius(string radiusStr)
    {
        GpsGenRadius = radiusStr;
        GpsGenRadiusUnit = "Metros (m)";
    }

    [RelayCommand]
    private void GpsGenSetRadiusKm(string radiusStr)
    {
        GpsGenRadius = radiusStr;
        GpsGenRadiusUnit = "Kilómetros (km)";
    }

    [RelayCommand]
    private void GpsGenUseCurrentFileGps()
    {
        if (SelectedFile != null && SelectedFile.GpsDecimal != "—")
        {
            var parsed = GpsGeneratorService.ParseCoordinates(SelectedFile.GpsDecimal);
            if (parsed.HasValue)
            {
                GpsGenCenterLat = parsed.Value.Lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                GpsGenCenterLon = parsed.Value.Lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                StatusText = "Punto central obtenido de la foto seleccionada ✓";
                return;
            }
        }
        StatusText = "El archivo seleccionado no contiene coordenadas GPS.";
    }

    [RelayCommand]
    private void GpsGenGeneratePreview()
    {
        if (double.TryParse(GpsGenCenterLat.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cLat) &&
            double.TryParse(GpsGenCenterLon.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double cLon) &&
            double.TryParse(GpsGenRadius.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double radius))
        {
            double radiusMeters = GpsGenRadiusUnit.Contains("Kilómetro") ? radius * 1000.0 : radius;
            var (lat, lon) = GpsGeneratorService.GenerateRandomCoordinate(cLat, cLon, radiusMeters);
            GpsGenPreviewLat = lat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            GpsGenPreviewLon = lon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            GpsGenPreviewDms = GpsGeneratorService.ToDms(lat, lon);
            HasGpsGenPreview = true;
            StatusText = $"Coordenada de prueba generada: {GpsGenPreviewLat}, {GpsGenPreviewLon} ✓";
        }
        else
        {
            StatusText = "Por favor ingresa valores válidos de Latitud, Longitud y Radio.";
        }
    }

    // ===== Format Conversion =====
    [ObservableProperty]
    private string _targetFormat = "JPG";

    partial void OnTargetFormatChanged(string value)
    {
        SaveCurrentState();
    }

    [ObservableProperty]
    private int _conversionQuality = 90;

    partial void OnConversionQualityChanged(int value)
    {
        SaveCurrentState();
    }

    public ObservableCollection<string> SupportedFormats { get; } = new();

    private static readonly string[] ImageFormats = { "JPG", "JPEG", "PNG", "WEBP", "BMP", "TIFF", "GIF", "ICO" };
    private static readonly string[] RawFormats = { "JPG", "JPEG", "PNG", "TIFF" };
    private static readonly string[] VideoFormats = { "MP4", "MKV", "AVI", "MOV" };
    private static readonly string[] AudioFormats = { "MP3", "WAV", "FLAC", "OGG", "AAC" };
    private static readonly string[] DocumentFormats = { "PDF", "TXT" };

    private string GetCategoryForExtension(string ext)
    {
        return ext.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".tiff" or ".bmp" or ".webp" or ".gif" => "Image",
            ".cr2" or ".nef" or ".arw" or ".dng" or ".raw" => "Raw",
            ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".flv" or ".webm" => "Video",
            ".mp3" or ".flac" or ".wav" or ".aac" or ".ogg" or ".m4a" => "Audio",
            ".pdf" or ".docx" or ".doc" or ".txt" or ".rtf" or ".csv" => "Document",
            _ => "Unknown"
        };
    }

    private bool AreCategoriesCompatible(string cat1, string cat2)
    {
        if (cat1 == cat2) return true;
        if ((cat1 == "Image" || cat1 == "Raw") && (cat2 == "Image" || cat2 == "Raw")) return true;
        return false;
    }

    private string[] GetFormatsForCategory(string category)
    {
        return category switch
        {
            "Image" => ImageFormats,
            "Raw" => RawFormats,
            "Video" => VideoFormats,
            "Audio" => AudioFormats,
            "Document" => DocumentFormats,
            _ => Array.Empty<string>()
        };
    }

    private void UpdateSupportedFormats()
    {
        var selectedFiles = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        
        SupportedFormats.Clear();

        if (selectedFiles.Count == 0)
        {
            // Unblock everything
            foreach (var file in Files)
            {
                file.IsCheckable = true;
            }

            // Union of all formats for all files in folder
            var allCategories = Files.Where(f => !f.IsDirectory)
                                     .Select(f => GetCategoryForExtension(f.Extension))
                                     .Distinct()
                                     .ToList();

            var unionFormats = new HashSet<string>();
            foreach(var cat in allCategories)
            {
                if (cat == "Unknown") continue;
                foreach(var fmt in GetFormatsForCategory(cat)) unionFormats.Add(fmt);
            }

            foreach (var fmt in unionFormats.OrderBy(f => f)) SupportedFormats.Add(fmt);
        }
        else
        {
            // Block incompatible checkboxes
            string dominantCategory = GetCategoryForExtension(selectedFiles.First().Extension);
            
            foreach (var file in Files)
            {
                if (file.IsDirectory) continue;
                string cat = GetCategoryForExtension(file.Extension);
                file.IsCheckable = AreCategoriesCompatible(dominantCategory, cat) || file.IsSelected; // Always allow unchecking
            }

            // If mixed (though UX prevents this now, but if somehow happens):
            bool isMixed = selectedFiles.Any(f => !AreCategoriesCompatible(dominantCategory, GetCategoryForExtension(f.Extension)));

            if (!isMixed && dominantCategory != "Unknown")
            {
                foreach (var fmt in GetFormatsForCategory(dominantCategory)) SupportedFormats.Add(fmt);
            }
        }

        if (SupportedFormats.Count > 0)
        {
            if (string.IsNullOrEmpty(TargetFormat) || !SupportedFormats.Contains(TargetFormat))
            {
                TargetFormat = SupportedFormats.First();
            }
        }
        else
        {
            TargetFormat = string.Empty;
        }
    }

    // ===== Resize Operation =====
    [ObservableProperty]
    private string _targetWidth = "1920";

    [ObservableProperty]
    private string _targetHeight = "1080";

    [ObservableProperty]
    private bool _keepAspectRatio = true;

    private double _originalAspectRatio = 1.0;
    private bool _isUpdatingDimensions = false;

    partial void OnTargetWidthChanged(string value)
    {
        if (!_isUpdatingDimensions && KeepAspectRatio && int.TryParse(value, out int w))
        {
            _isUpdatingDimensions = true;
            TargetHeight = ((int)Math.Round(w / _originalAspectRatio)).ToString();
            _isUpdatingDimensions = false;
        }
        SaveCurrentState();
    }

    partial void OnTargetHeightChanged(string value)
    {
        if (!_isUpdatingDimensions && KeepAspectRatio && int.TryParse(value, out int h))
        {
            _isUpdatingDimensions = true;
            TargetWidth = ((int)Math.Round(h * _originalAspectRatio)).ToString();
            _isUpdatingDimensions = false;
        }
        SaveCurrentState();
    }

    partial void OnKeepAspectRatioChanged(bool value)
    {
        if (value && !_isUpdatingDimensions)
        {
            if (int.TryParse(TargetWidth, out int w))
            {
                _isUpdatingDimensions = true;
                TargetHeight = ((int)Math.Round(w / _originalAspectRatio)).ToString();
                _isUpdatingDimensions = false;
            }
        }
        SaveCurrentState();
    }

    [ObservableProperty]
    private bool _batchRecursive;

    [ObservableProperty]
    private bool _isConfirmDialogOpen;

    [ObservableProperty]
    private int _batchProgress;

    [ObservableProperty]
    private string _batchStatusText = "Listo";

    [ObservableProperty]
    private bool _isBatchRunning;

    // ===== Status Bar =====
    [ObservableProperty]
    private string _statusText = "Listo";

    [ObservableProperty]
    private string _memoryUsage = "45 MB";

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private string _selectedFileName = string.Empty;

    [ObservableProperty]
    private bool _hasFileSelected;

    [ObservableProperty]
    private bool _isBackupDialogOpen;

    [ObservableProperty]
    private bool _hasDirectoryLoaded;

    [ObservableProperty]
    private FolderNode? _selectedFolderNode;

    partial void OnSelectedFolderNodeChanged(FolderNode? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.FullPath))
        {
            CurrentPath = value.FullPath;
            string? fileToSelect = _pendingSelectedFile;
            _pendingSelectedFile = null;
            _ = ReloadFilesFromPathAsync(value.FullPath, fileToSelect);
            SetupWatcher(value.FullPath);
            SaveCurrentState();
        }
    }

    partial void OnSelectedFileChanged(FileItem? value)
    {
        if (value != null)
        {
            HasFileSelected = true;
            SelectedFileName = value.Name;
            LoadDemoMetadata(value);

            if (!string.IsNullOrEmpty(value.Resolution) && value.Resolution != "—")
            {
                var parts = value.Resolution.Split(new[] { " × ", " px" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    if (double.TryParse(parts[0].Trim(), out double w) && double.TryParse(parts[1].Trim(), out double h) && h > 0)
                    {
                        _originalAspectRatio = w / h;
                        
                        _isUpdatingDimensions = true;
                        TargetWidth = w.ToString();
                        TargetHeight = h.ToString();
                        _isUpdatingDimensions = false;
                    }
                }
            }
        }
        else
        {
            HasFileSelected = false;
            SelectedFileName = string.Empty;
        }
    }

    public Func<Task<string?>>? RequestOpenFolderAsync;
    public Func<Task<string?>>? RequestSaveKmzAsync;
    private FileSystemWatcher? _watcher;

    public MainViewModel()
    {
        Console.WriteLine("MainViewModel: Constructor started");
        foreach (var t in MetadataTemplateRegistry.Instance.Templates)
        {
            AvailableTemplates.Add(t);
        }
        SelectedTemplate = AvailableTemplates.FirstOrDefault();

        foreach (var d in PhoneDeviceRegistry.Instance.Devices)
        {
            AvailablePhoneDevices.Add(d);
        }
        SelectedPhoneDevice = AvailablePhoneDevices.FirstOrDefault();

        Console.WriteLine("MainViewModel: Constructor completed");
    }

    private bool _isCalculatingGps = false;

    public void CalculateGpsStatistics(bool useCustomRef = false)
    {
        if (_isCalculatingGps) return;
        _isCalculatingGps = true;

        try
        {
            if (Files == null || Files.Count == 0)
            {
                if (!useCustomRef)
                {
                    GpsRefLat = "—";
                    GpsRefLon = "—";
                }
                GpsMinDistance = "—";
                GpsMaxDistance = "—";
                GpsAverageDistance = "—";
                return;
            }

            var filesWithGps = new List<(FileItem file, double lat, double lon)>();
            foreach (var f in Files)
            {
                if (f.GpsDecimal != "—")
                {
                    var parsed = GpsGeneratorService.ParseCoordinates(f.GpsDecimal);
                    if (parsed.HasValue)
                    {
                        filesWithGps.Add((f, parsed.Value.Lat, parsed.Value.Lon));
                    }
                }
            }

            if (filesWithGps.Count == 0)
            {
                if (!useCustomRef)
                {
                    GpsRefLat = "—";
                    GpsRefLon = "—";
                }
                GpsMinDistance = "—";
                GpsMaxDistance = "—";
                GpsAverageDistance = "—";
                foreach (var f in Files) f.GpsDistanceToMidpoint = "—";
                return;
            }

            double refLat = 0, refLon = 0;

            if (!useCustomRef)
            {
                double sumLat = 0, sumLon = 0;
                foreach (var item in filesWithGps)
                {
                    sumLat += item.lat;
                    sumLon += item.lon;
                }

                refLat = sumLat / filesWithGps.Count;
                refLon = sumLon / filesWithGps.Count;

                GpsRefLat = refLat.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
                GpsRefLon = refLon.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                // Attempt to parse custom coordinates
                var parsed = GpsGeneratorService.ParseCoordinates($"{GpsRefLat}, {GpsRefLon}");
                if (parsed.HasValue)
                {
                    refLat = parsed.Value.Lat;
                    refLon = parsed.Value.Lon;
                }
                else
                {
                    // Invalid input, clear stats
                    GpsMinDistance = "—";
                    GpsMaxDistance = "—";
                    GpsAverageDistance = "—";
                    foreach (var f in Files) f.GpsDistanceToMidpoint = "—";
                    return;
                }
            }

            double totalDistance = 0;
            double minDist = double.MaxValue;
            double maxDist = double.MinValue;

            foreach (var item in filesWithGps)
            {
                double dist = GpsGeneratorService.CalculateHaversineDistance(refLat, refLon, item.lat, item.lon);
                totalDistance += dist;
                
                if (dist < minDist) minDist = dist;
                if (dist > maxDist) maxDist = dist;
                
                if (dist < 1000)
                    item.file.GpsDistanceToMidpoint = $"{Math.Round(dist, 1)} m";
                else
                    item.file.GpsDistanceToMidpoint = $"{Math.Round(dist / 1000.0, 2)} km";
            }

            foreach (var f in Files.Except(filesWithGps.Select(x => x.file)))
            {
                f.GpsDistanceToMidpoint = "—";
            }

            double avgDist = totalDistance / filesWithGps.Count;
            
            GpsMinDistance = minDist < 1000 ? $"{Math.Round(minDist, 1)} m" : $"{Math.Round(minDist / 1000.0, 2)} km";
            GpsMaxDistance = maxDist < 1000 ? $"{Math.Round(maxDist, 1)} m" : $"{Math.Round(maxDist / 1000.0, 2)} km";
            GpsAverageDistance = avgDist < 1000 ? $"{Math.Round(avgDist, 1)} m" : $"{Math.Round(avgDist / 1000.0, 2)} km";
        }
        finally
        {
            _isCalculatingGps = false;
        }
    }

    public void EvaluateAllFilesCompliance()
    {
        if (Files == null || Files.Count == 0) return;

        if (SelectedTemplate == null || SelectedTemplate.Id == "None")
        {
            var noneResult = new TemplateComplianceResult
            {
                State = ComplianceState.None,
                SummaryMessage = "—"
            };

            foreach (var file in Files)
            {
                file.SetComplianceResult(noneResult);
            }

            CompliantCount = 0;
            NonCompliantCount = 0;
            IncompleteCount = 0;
            return;
        }

        var template = SelectedTemplate;
        var filesList = Files.ToList();

        Task.Run(() =>
        {
            int compliant = 0;
            int nonCompliant = 0;
            int incomplete = 0;

            foreach (var file in filesList)
            {
                if (file.IsDirectory) continue;
                var result = template.ValidateCompliance(file);
                if (result.State == ComplianceState.Compliant) compliant++;
                else if (result.State == ComplianceState.NonCompliant) nonCompliant++;
                else if (result.State == ComplianceState.Incomplete) incomplete++;

                Dispatcher.UIThread.InvokeAsync(() => file.SetComplianceResult(result));
            }

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CompliantCount = compliant;
                NonCompliantCount = nonCompliant;
                IncompleteCount = incomplete;
            });
        });
    }

    [RelayCommand]
    private void ReevaluateTemplates()
    {
        EvaluateAllFilesCompliance();
        StatusText = $"Verificación de plantilla '{SelectedTemplate?.Name}' actualizada ✓";
    }

    // ===== Commands =====
    [RelayCommand]
    private void NavigateTool(string toolName)
    {
        ActiveTool = toolName;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task OpenFolder()
    {
        if (RequestOpenFolderAsync != null)
        {
            var folder = await RequestOpenFolderAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                LoadRealData(folder);
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task GenerateKmz()
    {
        var selectedFilesWithGps = Files?.Where(f => f.IsSelected && !string.IsNullOrEmpty(f.GpsDecimal) && f.GpsDecimal != "—").ToList();

        if (selectedFilesWithGps == null || selectedFilesWithGps.Count == 0)
        {
            StatusText = "No hay archivos seleccionados con coordenadas GPS válidas.";
            return;
        }

        if (RequestSaveKmzAsync == null) return;

        double refLat = 0, refLon = 0;
        bool hasValidRef = false;
        
        if (KmzEnableComparison)
        {
            var parsed = GpsGeneratorService.ParseCoordinates($"{GpsRefLat}, {GpsRefLon}");
            if (parsed.HasValue)
            {
                refLat = parsed.Value.Lat;
                refLon = parsed.Value.Lon;
                hasValidRef = true;
                
                double minDist = double.MaxValue;
                double maxDist = double.MinValue;
                double sumDist = 0;
                int count = 0;
                
                foreach(var file in selectedFilesWithGps)
                {
                    var fileGps = GpsGeneratorService.ParseCoordinates(file.GpsDecimal);
                    if(fileGps.HasValue)
                    {
                        double dist = GpsGeneratorService.CalculateHaversineDistance(refLat, refLon, fileGps.Value.Lat, fileGps.Value.Lon);
                        if (dist < minDist) minDist = dist;
                        if (dist > maxDist) maxDist = dist;
                        sumDist += dist;
                        count++;
                    }
                }
                
                if (count > 0)
                {
                    double avgDist = sumDist / count;
                    KmzMinDist = minDist < 1000 ? $"{Math.Round(minDist, 1)} m" : $"{Math.Round(minDist / 1000.0, 2)} km";
                    KmzMaxDist = maxDist < 1000 ? $"{Math.Round(maxDist, 1)} m" : $"{Math.Round(maxDist / 1000.0, 2)} km";
                    KmzAvgDist = avgDist < 1000 ? $"{Math.Round(avgDist, 1)} m" : $"{Math.Round(avgDist / 1000.0, 2)} km";
                    KmzStatsVisible = true;
                }
                else
                {
                    KmzStatsVisible = false;
                }
            }
            else
            {
                StatusText = "La coordenada de referencia no es válida.";
                return;
            }
        }
        else
        {
            KmzStatsVisible = false;
        }

        var savePath = await RequestSaveKmzAsync();
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            StatusText = $"Generando KMZ con {selectedFilesWithGps.Count} ubicaciones...";

            string kmlContent = GenerateKmlContent(selectedFilesWithGps, hasValidRef, refLat, refLon);

            using (var fs = new FileStream(savePath, FileMode.Create))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                var kmlEntry = archive.CreateEntry("doc.kml");
                using (var entryStream = kmlEntry.Open())
                using (var writer = new StreamWriter(entryStream, System.Text.Encoding.UTF8))
                {
                    await writer.WriteAsync(kmlContent);
                }
            }

            StatusText = $"KMZ guardado exitosamente en: {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al generar KMZ: {ex.Message}";
        }
    }

    private string GetRefSymbolUrl()
    {
        return KmzRefSymbol switch
        {
            "Estrella" => "http://maps.google.com/mapfiles/kml/shapes/star.png",
            "Círculo" => "http://maps.google.com/mapfiles/kml/shapes/placemark_circle.png",
            "Chincheta" => "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png",
            "Cuadrado" => "http://maps.google.com/mapfiles/kml/shapes/placemark_square.png",
            "Triángulo" => "http://maps.google.com/mapfiles/kml/shapes/triangle.png",
            _ => "http://maps.google.com/mapfiles/kml/shapes/star.png"
        };
    }

    private string GenerateKmlContent(List<FileItem> files, bool hasRef = false, double refLat = 0, double refLon = 0)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine("  <Document>");
        sb.AppendLine("    <name>Exportación KMZ MegaDatos</name>");

        string kmlColor = KmzMarkerColor switch
        {
            "Rojo" => "ff0000ff",
            "Azul" => "ffff0000",
            "Verde" => "ff00ff00",
            "Amarillo" => "ff00ffff",
            "Blanco" => "ffffffff",
            _ => "ff0000ff"
        };

        string iconUrl = "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png";

        sb.AppendLine("    <Style id=\"customStyle\">");
        sb.AppendLine("      <IconStyle>");
        sb.AppendLine($"        <color>{kmlColor}</color>");
        sb.AppendLine("        <scale>1.0</scale>");
        sb.AppendLine("        <Icon>");
        sb.AppendLine($"          <href>{iconUrl}</href>");
        sb.AppendLine("        </Icon>");
        sb.AppendLine("      </IconStyle>");
        sb.AppendLine("      <LineStyle>");
        sb.AppendLine($"        <color>{kmlColor}</color>");
        sb.AppendLine("        <width>3</width>");
        sb.AppendLine("      </LineStyle>");
        sb.AppendLine("    </Style>");

        if (hasRef && KmzEnableComparison)
        {
            if (KmzIncludeRefPoint)
            {
                sb.AppendLine("    <Placemark>");
                sb.AppendLine("      <name>Coordenada de Referencia</name>");
                sb.AppendLine("      <Style>");
                sb.AppendLine("        <IconStyle>");
                sb.AppendLine("          <color>ff00ffff</color>"); // Yellow
                sb.AppendLine("          <scale>1.5</scale>");
                sb.AppendLine("          <Icon>");
                sb.AppendLine($"            <href>{GetRefSymbolUrl()}</href>");
                sb.AppendLine("          </Icon>");
                sb.AppendLine("        </IconStyle>");
                sb.AppendLine("      </Style>");
                sb.AppendLine("      <Point>");
                sb.AppendLine($"        <coordinates>{refLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{refLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}</coordinates>");
                sb.AppendLine("      </Point>");
                sb.AppendLine("    </Placemark>");
            }

            if (KmzDrawCircle && double.TryParse(KmzCircleRadius.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double radius) && radius > 0)
            {
                sb.AppendLine("    <Placemark>");
                sb.AppendLine($"      <name>Radio de {radius} m</name>");
                sb.AppendLine("      <Style>");
                sb.AppendLine("        <LineStyle>");
                sb.AppendLine("          <color>8800ffff</color>");
                sb.AppendLine("          <width>2</width>");
                sb.AppendLine("        </LineStyle>");
                sb.AppendLine("        <PolyStyle>");
                sb.AppendLine("          <color>3300ffff</color>");
                sb.AppendLine("        </PolyStyle>");
                sb.AppendLine("      </Style>");
                sb.AppendLine("      <Polygon>");
                sb.AppendLine("        <outerBoundaryIs>");
                sb.AppendLine("          <LinearRing>");
                sb.AppendLine("            <coordinates>");
                
                for (int i = 0; i <= 360; i += 10)
                {
                    double angle = i * (Math.PI / 180.0);
                    double deltaLat = (radius * Math.Cos(angle)) / 111139.0;
                    double centerLatRad = refLat * (Math.PI / 180.0);
                    double cosLat = Math.Cos(centerLatRad);
                    if (Math.Abs(cosLat) < 1e-6) cosLat = 1e-6;
                    double deltaLon = (radius * Math.Sin(angle)) / (111139.0 * cosLat);
                    
                    double newLat = Math.Clamp(refLat + deltaLat, -90.0, 90.0);
                    double newLon = Math.Clamp(refLon + deltaLon, -180.0, 180.0);
                    
                    sb.AppendLine($"              {newLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{newLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
                
                sb.AppendLine("            </coordinates>");
                sb.AppendLine("          </LinearRing>");
                sb.AppendLine("        </outerBoundaryIs>");
                sb.AppendLine("      </Polygon>");
                sb.AppendLine("    </Placemark>");
            }
        }

        var sortedFiles = files.OrderBy(f => f.DateModified).ToList();
        var validCoordinates = new List<string>();

        foreach (var file in sortedFiles)
        {
            var parts = file.GpsDecimal.Split(',');
            if (parts.Length == 2 && 
                double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
            {
                string coord = $"{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                validCoordinates.Add(coord);

                sb.AppendLine("    <Placemark>");
                
                string title = KmzIncludeFilename ? EscapeXml(file.Name) : "Marcador";
                sb.AppendLine($"      <name>{title}</name>");
                sb.AppendLine("      <styleUrl>#customStyle</styleUrl>");
                
                if (KmzIncludeDate)
                {
                    string dateStr = file.DateModified.ToString("yyyy-MM-dd HH:mm:ss");
                    sb.AppendLine($"      <description>Fecha: {dateStr}</description>");
                }
                
                sb.AppendLine("      <Point>");
                sb.AppendLine($"        <coordinates>{coord}</coordinates>");
                sb.AppendLine("      </Point>");
                sb.AppendLine("    </Placemark>");
            }
        }

        if (KmzIncludePath && validCoordinates.Count > 1)
        {
            sb.AppendLine("    <Placemark>");
            sb.AppendLine("      <name>Ruta (Trazo de Recorrido)</name>");
            sb.AppendLine("      <styleUrl>#customStyle</styleUrl>");
            sb.AppendLine("      <LineString>");
            sb.AppendLine("        <tessellate>1</tessellate>");
            sb.AppendLine("        <coordinates>");
            foreach (var coord in validCoordinates)
            {
                sb.AppendLine($"          {coord}");
            }
            sb.AppendLine("        </coordinates>");
            sb.AppendLine("      </LineString>");
            sb.AppendLine("    </Placemark>");
        }

        sb.AppendLine("  </Document>");
        sb.AppendLine("</kml>");
        
        return sb.ToString();
    }

    private string EscapeXml(string unescaped)
    {
        if (string.IsNullOrEmpty(unescaped)) return string.Empty;
        return unescaped.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
    }

    [RelayCommand]
    private void StripMetadata()
    {
        ActiveTool = "Clean";
        ExecuteBatch();
    }

    [RelayCommand]
    private void SaveMetadata()
    {
        IsBackupDialogOpen = true;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConfirmSave(object withBackupStr)
    {
        bool withBackup = withBackupStr?.ToString() == "True";
        IsBackupDialogOpen = false;

        if (SelectedFile == null) return;
        StatusText = "Guardando metadatos...";

        string exiftoolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "exiftool(-k).exe");
        if (!File.Exists(exiftoolPath))
        {
            exiftoolPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "exiftool(-k).exe");
        }

        var args = new List<string>();
        if (!withBackup) args.Add("-overwrite_original");

        // Helper to map UI names to ExifTool tags
        string MapToExifTag(string group, string key)
        {
            if (group == "General")
            {
                if (key == "Nombre") return "FileName";
            }
            if (group == "GPS")
            {
                if (key == "Latitud") return "GPSLatitude";
                if (key == "Longitud") return "GPSLongitude";
                if (key == "Altitud") return "GPSAltitude";
            }
            if (group == "EXIF")
            {
                if (key == "Marca") return "Make";
                if (key == "Modelo") return "Model";
                if (key == "Lente") return "LensModel";
                if (key == "Apertura") return "FNumber";
                if (key == "Velocidad") return "ExposureTime";
                if (key == "ISO") return "ISO";
                if (key == "Distancia Focal") return "FocalLength";
                if (key == "Flash") return "Flash";
                if (key == "Modo Exposición") return "ExposureProgram";
                if (key == "Balance Blancos") return "WhiteBalance";
            }
            if (group == "Autoría")
            {
                if (key == "Autor") return "Artist"; // or Creator
                if (key == "Copyright") return "Copyright";
                if (key == "Software") return "Software";
                if (key == "Calificación") return "Rating";
            }
            if (group == "IPTC/XMP")
            {
                if (key == "Título") return "Title";
                if (key == "Descripción") return "Description";
                if (key == "Palabras Clave") return "Subject";
            }
            return "";
        }

        var allEntries = GeneralInfo.Concat(GpsData).Concat(ExifData).Concat(IptcData).Concat(AuthorshipData);

        bool changesMade = false;
        foreach (var entry in allEntries)
        {
            if (entry.IsEditable && entry.Value != entry.OriginalValue)
            {
                string tag = MapToExifTag(entry.Group, entry.Key);
                if (!string.IsNullOrEmpty(tag))
                {
                    string cleanVal = entry.Value;
                    if (entry.Key == "Calificación") cleanVal = entry.Value.Count(c => c == '★').ToString();
                    
                    if (entry.IsGpsCoordinate)
                    {
                        // Extract hemisphere from the MetadataEntry
                        string hem = entry.Hemisphere;
                        
                        // Get the absolute numeric value
                        string numericVal = cleanVal
                            .Replace("°", "").Replace("'", "").Replace("\"", "")
                            .Replace("N", "").Replace("S", "").Replace("E", "").Replace("W", "")
                            .Trim();
                        
                        // Send the coordinate value as positive (absolute)
                        if (double.TryParse(numericVal, System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, out double coordVal))
                        {
                            numericVal = Math.Abs(coordVal).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        
                        args.Add($"-{tag}={numericVal}");
                        
                        // Send the hemisphere reference tag
                        if (entry.Key == "Latitud")
                        {
                            string latRef = (hem == "S") ? "S" : "N";
                            args.Add($"-GPSLatitudeRef={latRef}");
                        }
                        else if (entry.Key == "Longitud")
                        {
                            string lonRef = (hem == "W") ? "W" : "E";
                            args.Add($"-GPSLongitudeRef={lonRef}");
                        }
                    }
                    else
                    {
                        args.Add($"-{tag}={cleanVal}");
                    }
                    
                    changesMade = true;
                }
            }
        }

        if (changesMade)
        {
            try
            {
                args.Add(SelectedFile.FullPath);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exiftoolPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                foreach (var arg in args)
                {
                    psi.ArgumentList.Add(arg);
                }

                int maxRetries = 5;
                bool success = false;
                string lastErrors = "";
                string lastOutput = "";
                
                for (int i = 0; i < maxRetries; i++)
                {
                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        var errTask = process.StandardError.ReadToEndAsync();
                        var outTask = process.StandardOutput.ReadToEndAsync();
                        
                        await System.Threading.Tasks.Task.WhenAll(errTask, outTask, process.WaitForExitAsync());
                        
                        lastErrors = errTask.Result;
                        lastOutput = outTask.Result;
                        
                        if (process.ExitCode == 0)
                        {
                            success = true;
                            break;
                        }
                    }
                    await System.Threading.Tasks.Task.Delay(500);
                }

                if (success)
                {
                    StatusText = withBackup ? "Metadatos guardados (Copia de seguridad .bak creada) ✓" : "Metadatos guardados (Sin copia de seguridad) ✓";
                    
                    string selectedPath = SelectedFile.FullPath;
                    _ = ReloadFilesFromPathAsync(CurrentPath, selectedPath);
                }
                else
                {
                    string msg = string.IsNullOrWhiteSpace(lastErrors) ? lastOutput : lastErrors;
                    StatusText = $"Error al guardar tras reintentos: {msg}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }
        else
        {
            StatusText = "No se detectaron campos válidos para guardar.";
        }
    }

    [RelayCommand]
    private void CancelSave()
    {
        IsBackupDialogOpen = false;
        StatusText = "Guardado cancelado";
    }

    [RelayCommand]
    private void UndoChanges()
    {
        if (SelectedFile != null)
        {
            LoadDemoMetadata(SelectedFile);
            StatusText = "Cambios de metadatos revertidos ✓";
        }
    }

    [RelayCommand]
    private void ClearGps()
    {
        GpsLatitude = string.Empty;
        GpsLongitude = string.Empty;
        GpsAltitude = string.Empty;
        StatusText = "Coordenadas GPS eliminadas ✓";
    }

    [RelayCommand]
    private void ExecuteBatch()
    {
        if (ActiveTool == "Templates" && (SelectedTemplate == null || SelectedTemplate.Id == "None"))
        {
            StatusText = "Selecciona una plantilla válida para procesar.";
            return;
        }

        var filesToProcess = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        if (filesToProcess.Count == 0 && SelectedFile != null && !SelectedFile.IsDirectory)
        {
            filesToProcess.Add(SelectedFile);
        }

        if (filesToProcess.Count == 0)
        {
            StatusText = "Selecciona al menos un archivo para procesar.";
            return;
        }

        // Show confirmation dialog overlay instead of processing immediately
        IsConfirmDialogOpen = true;
    }

    [RelayCommand]
    private void CancelBatch()
    {
        IsConfirmDialogOpen = false;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConfirmBatchAsync(object overwriteParam)
    {
        IsConfirmDialogOpen = false;
        bool overwriteOriginal = overwriteParam?.ToString() == "True";

        var filesToProcess = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        if (filesToProcess.Count == 0 && SelectedFile != null && !SelectedFile.IsDirectory)
        {
            filesToProcess.Add(SelectedFile);
        }
        if (filesToProcess.Count == 0) return;

        IsBatchRunning = true;
        BatchProgress = 0;
        BatchStatusText = "Procesando...";
        StatusText = $"Procesamiento iniciado ({filesToProcess.Count} archivos)...";

        await Task.Run(async () =>
        {
            int count = 0;
            (double Lat, double Lon)? sharedGps = null;
            bool hasCriticalError = false;
            string criticalErrorMessage = "";

            foreach (var file in filesToProcess)
            {
                if (hasCriticalError) break;

                try
                {
                    if (GetCategoryForExtension(file.Extension) == "Image" || GetCategoryForExtension(file.Extension) == "Raw")
                    {
                        using var image = new MagickImage(file.FullPath);
                        var originalInterlace = image.Interlace;

                        if (ActiveTool == "Clean")
                        {
                            // Rotar físicamente los píxeles basados en la etiqueta EXIF de orientación
                            // ANTES de borrar los metadatos, para que la imagen no quede volteada.
                            image.AutoOrient();

                            if (CleanAllMetadata)
                            {
                                image.Strip();
                            }
                            else
                            {
                                if (CleanExif) image.RemoveProfile("exif");
                                if (CleanIptc) image.RemoveProfile("iptc");
                                if (CleanAuthorship)
                                {
                                    image.RemoveProfile("8bim");
                                    image.RemoveProfile("xmp");
                                }
                                if (CleanGps)
                                {
                                    var exif = image.GetExifProfile();
                                    if (exif != null)
                                    {
                                        exif.RemoveValue(ExifTag.GPSLatitude);
                                        exif.RemoveValue(ExifTag.GPSLatitudeRef);
                                        exif.RemoveValue(ExifTag.GPSLongitude);
                                        exif.RemoveValue(ExifTag.GPSLongitudeRef);
                                        exif.RemoveValue(ExifTag.GPSAltitude);
                                        exif.RemoveValue(ExifTag.GPSAltitudeRef);
                                        exif.RemoveValue(ExifTag.GPSVersionID);
                                        exif.RemoveValue(ExifTag.GPSTimestamp);
                                        exif.RemoveValue(ExifTag.GPSDateStamp);
                                        exif.RemoveValue(ExifTag.GPSImgDirection);
                                        exif.RemoveValue(ExifTag.GPSImgDirectionRef);
                                        exif.RemoveValue(ExifTag.GPSDestBearing);
                                        exif.RemoveValue(ExifTag.GPSDestBearingRef);
                                        exif.RemoveValue(ExifTag.GPSMapDatum);
                                        exif.RemoveValue(ExifTag.GPSProcessingMethod);
                                        image.SetProfile(exif);
                                    }
                                }
                            }

                            string newName = overwriteOriginal 
                                ? Path.GetFileName(file.FullPath)
                                : Path.GetFileNameWithoutExtension(file.FullPath) + "_limpio" + file.Extension;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                            if (overwriteOriginal && BatchBackupEnabled)
                            {
                                string backupPath = file.FullPath + ".bak";
                                try
                                {
                                    if (!File.Exists(backupPath))
                                    {
                                        File.Copy(file.FullPath, backupPath, true);
                                    }
                                }
                                catch { }
                            }

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);
                            if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                            {
                                if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                else image.Format = MagickFormat.Jpeg;
                            }

                            image.Write(newPath);

                            try
                            {
                                File.SetCreationTime(newPath, creationTime);
                                File.SetLastWriteTime(newPath, lastWriteTime);
                            }
                            catch { }
                        }
                        else if (ActiveTool == "Templates" && SelectedTemplate != null)
                        {
                            string newName = overwriteOriginal 
                                ? Path.GetFileName(file.FullPath)
                                : Path.GetFileNameWithoutExtension(file.FullPath) + $"_{SelectedTemplate.Id.ToLowerInvariant()}" + file.Extension;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                            if (overwriteOriginal && BatchBackupEnabled)
                            {
                                string backupPath = file.FullPath + ".bak";
                                try
                                {
                                    if (!File.Exists(backupPath))
                                    {
                                        File.Copy(file.FullPath, backupPath, true);
                                    }
                                }
                                catch { }
                            }

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);

                            if (SelectedTemplate.UsesExifTool)
                            {
                                if (!overwriteOriginal && file.FullPath != newPath)
                                {
                                    File.Copy(file.FullPath, newPath, true);
                                }
                                
                                // Free file handle so ExifTool can modify it
                                image.Dispose(); 
                                
                                await SelectedTemplate.ApplyWithExifToolAsync(newPath, file);
                            }
                            else
                            {
                                SelectedTemplate.Apply(image, file);
                                if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                                {
                                    if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                    else image.Format = MagickFormat.Jpeg;
                                }
                                image.Write(newPath);
                            }

                            try
                            {
                                File.SetCreationTime(newPath, creationTime);
                                File.SetLastWriteTime(newPath, lastWriteTime);
                            }
                            catch { }
                        }
                        else if (ActiveTool == "Phone" && SelectedPhoneDevice != null)
                        {
                            string newName = overwriteOriginal 
                                ? Path.GetFileName(file.FullPath)
                                : Path.GetFileNameWithoutExtension(file.FullPath) + $"_{SelectedPhoneDevice.Id}" + file.Extension;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                            if (overwriteOriginal && BatchBackupEnabled)
                            {
                                string backupPath = file.FullPath + ".bak";
                                try
                                {
                                    if (!File.Exists(backupPath))
                                    {
                                        File.Copy(file.FullPath, backupPath, true);
                                    }
                                }
                                catch { }
                            }

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);

                            // Si no vamos a sobreescribir el original, copiamos la imagen base al nuevo destino primero
                            if (!overwriteOriginal)
                            {
                                File.Copy(file.FullPath, newPath, true);
                            }

                            // Aplicamos ExifTool sobre el archivo de destino (newPath si es copia, file.FullPath si es original)
                            string targetToProcess = overwriteOriginal ? file.FullPath : newPath;
                            
                            // Cerramos MagickImage antes de lanzar ExifTool si comparten el mismo archivo
                            image.Dispose(); 

                            try
                            {
                                await SelectedPhoneDevice.ApplyWithExifToolAsync(targetToProcess, file, PhoneOverwriteAll, PhonePreserveGps);

                                try
                                {
                                    File.SetCreationTime(targetToProcess, creationTime);
                                    File.SetLastWriteTime(targetToProcess, lastWriteTime);
                                }
                                catch { }
                            }
                            catch (Exception ex)
                            {
                                hasCriticalError = true;
                                criticalErrorMessage = ex.Message;
                                break;
                            }
                        }
                        else if (ActiveTool == "GpsGen")
                        {
                            if (double.TryParse(GpsGenCenterLat.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double cLat) &&
                                double.TryParse(GpsGenCenterLon.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double cLon) &&
                                double.TryParse(GpsGenRadius.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double radiusVal))
                            {
                                double radiusMeters = GpsGenRadiusUnit.Contains("Kilómetro") ? radiusVal * 1000.0 : radiusVal;
                                double? alt = double.TryParse(GpsGenBaseAltitude.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedAlt) ? parsedAlt : null;

                                (double Lat, double Lon) targetCoord;
                                if (GpsGenUniquePerPhoto)
                                {
                                    targetCoord = GpsGeneratorService.GenerateRandomCoordinate(cLat, cLon, radiusMeters);
                                }
                                else
                                {
                                    if (!sharedGps.HasValue)
                                    {
                                        sharedGps = GpsGeneratorService.GenerateRandomCoordinate(cLat, cLon, radiusMeters);
                                    }
                                    targetCoord = sharedGps.Value;
                                }

                                double targetLat = targetCoord.Lat;
                                double targetLon = targetCoord.Lon;

                                double? finalAlt = alt.HasValue ? alt.Value + (Random.Shared.NextDouble() * 10.0 - 5.0) : null;

                                GpsGeneratorService.ApplyGpsToImage(image, targetLat, targetLon, finalAlt);

                                string newName = overwriteOriginal 
                                    ? Path.GetFileName(file.FullPath)
                                    : Path.GetFileNameWithoutExtension(file.FullPath) + "_gps" + file.Extension;
                                string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                                if (overwriteOriginal && BatchBackupEnabled)
                                {
                                    string backupPath = file.FullPath + ".bak";
                                    try
                                    {
                                        if (!File.Exists(backupPath))
                                        {
                                            File.Copy(file.FullPath, backupPath, true);
                                        }
                                    }
                                    catch { }
                                }

                                var creationTime = File.GetCreationTime(file.FullPath);
                                var lastWriteTime = File.GetLastWriteTime(file.FullPath);
                                if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                                {
                                    if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                    else image.Format = MagickFormat.Jpeg;
                                }

                                image.Write(newPath);

                                try
                                {
                                    File.SetCreationTime(newPath, creationTime);
                                    File.SetLastWriteTime(newPath, lastWriteTime);
                                }
                                catch { }
                            }
                        }
                        else if (ActiveTool == "Resize")
                        {
                            if (int.TryParse(TargetWidth, out int tw) && int.TryParse(TargetHeight, out int th))
                            {
                                var size = new MagickGeometry((uint)tw, (uint)th);
                                size.IgnoreAspectRatio = !KeepAspectRatio;
                                image.Resize(size);
                                
                                // Sincronización forense: Actualizar dimensiones internas en el perfil EXIF
                                // Nota: Al modificar el perfil EXIF, Magick.NET lo re-serializa por defecto en Little-endian (Intel, II),
                                // perdiendo el ExifByteOrder original (Big-endian, MM).
                                // Si se prefiere mantener el ByteOrder original intacto, es mejor no modificar el perfil aquí.
                                /*
                                var exif = image.GetExifProfile();
                                if (exif != null)
                                {
                                    exif.SetValue(ExifTag.PixelXDimension, new Number((uint)image.Width));
                                    exif.SetValue(ExifTag.PixelYDimension, new Number((uint)image.Height));

                                    if (exif.GetValue(ExifTag.ImageWidth) != null)
                                    {
                                        exif.SetValue(ExifTag.ImageWidth, new Number((uint)image.Width));
                                    }
                                    if (exif.GetValue(ExifTag.ImageLength) != null)
                                    {
                                        exif.SetValue(ExifTag.ImageLength, new Number((uint)image.Height));
                                    }

                                    image.SetProfile(exif);
                                }
                                */

                                string newName = overwriteOriginal 
                                    ? Path.GetFileName(file.FullPath)
                                    : Path.GetFileNameWithoutExtension(file.FullPath) + $"_{tw}x{th}" + file.Extension;
                                string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                                var creationTime = File.GetCreationTime(file.FullPath);
                                var lastWriteTime = File.GetLastWriteTime(file.FullPath);
                                if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                                {
                                    if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                    else image.Format = MagickFormat.Jpeg;
                                }

                                image.Write(newPath);

                                try {
                                    File.SetCreationTime(newPath, creationTime);
                                    File.SetLastWriteTime(newPath, lastWriteTime);
                                } catch { }
                            }
                        }
                        else if (ActiveTool == "Format")
                        {
                            string newExt = TargetFormat.ToLowerInvariant();
                            if (!newExt.StartsWith(".")) newExt = "." + newExt;

                            string newName = overwriteOriginal
                                ? Path.GetFileNameWithoutExtension(file.FullPath) + newExt
                                : Path.GetFileNameWithoutExtension(file.FullPath) + "_convertido" + newExt;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);
                            
                            if (newExt == ".jpg" || newExt == ".jpeg") image.Format = MagickFormat.Jpeg;
                            else if (newExt == ".png") image.Format = MagickFormat.Png;
                            else if (newExt == ".webp") image.Format = MagickFormat.WebP;
                            else if (newExt == ".bmp") image.Format = MagickFormat.Bmp;
                            else if (newExt == ".tiff") image.Format = MagickFormat.Tiff;

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);
                            if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                            {
                                if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                            }
                            
                            image.Write(newPath);
                            
                            try {
                                File.SetCreationTime(newPath, creationTime);
                                File.SetLastWriteTime(newPath, lastWriteTime);
                            } catch { }
                            
                            if (overwriteOriginal && !string.Equals(file.FullPath, newPath, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(file.FullPath); } catch { }
                            }
                        }
                    }
                }
                catch { }

                count++;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    BatchProgress = (int)((count / (double)filesToProcess.Count) * 100);
                    BatchStatusText = $"Procesando {count}/{filesToProcess.Count}...";
                });
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBatchRunning = false;
                BatchStatusText = "Listo";
                if (hasCriticalError)
                {
                    StatusText = "❌ Error crítico: " + criticalErrorMessage;
                }
                else
                {
                    StatusText = $"Proceso terminado. {filesToProcess.Count} archivos procesados ✓";
                }
                _ = ReloadFilesFromPathAsync(CurrentPath);
            });
        });
    }


    [RelayCommand]
    private void RefreshFiles()
    {
        StatusText = "Actualizando...";
        _ = ReloadFilesFromPathAsync(CurrentPath);
    }

    [RelayCommand]
    private void SelectFile(FileItem? file)
    {
        if (file == null) return;
        SelectedFile = file;
        HasFileSelected = true;
        SelectedFileName = file.Name;
        // SelectedCount is handled by checkbox property changed event.
        LoadDemoMetadata(file);

        if (!string.IsNullOrEmpty(file.Resolution) && file.Resolution != "—")
        {
            var parts = file.Resolution.Split(new[] { " × ", " px" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                if (double.TryParse(parts[0].Trim(), out double w) && double.TryParse(parts[1].Trim(), out double h) && h > 0)
                {
                    _originalAspectRatio = w / h;
                    
                    _isUpdatingDimensions = true;
                    TargetWidth = w.ToString();
                    TargetHeight = h.ToString();
                    _isUpdatingDimensions = false;
                }
            }
        }

        SaveCurrentState();
    }

    private void LoadDemoMetadata(FileItem file)
    {
        GeneralInfo.Clear();
        
        // Read real filesystem dates
        var fileCreation = File.GetCreationTime(file.FullPath);
        
        GeneralInfo.Add(new() { Key = "Nombre", Value = file.Name, Group = "General", IsEditable = true });
        GeneralInfo.Add(new() { Key = "Ruta", Value = file.FullPath, Group = "General", IsEditable = false });
        GeneralInfo.Add(new() { Key = "Tamaño", Value = file.SizeFormatted, Group = "General", IsEditable = false });
        GeneralInfo.Add(new() { Key = "Tipo", Value = file.FileType, Group = "General", IsEditable = false });
        GeneralInfo.Add(new() { Key = "Modificado", Value = file.DateModified.ToString("dd/MM/yyyy HH:mm"), Group = "General", IsEditable = false });
        GeneralInfo.Add(new() { Key = "Creado", Value = fileCreation.ToString("dd/MM/yyyy HH:mm"), Group = "General", IsEditable = false });

        GpsData.Clear();
        IptcData.Clear();
        ExifData.Clear();
        AuthorshipData.Clear();

        string lat = "";
        string lon = "";
        if (file.GpsDecimal != "—")
        {
            var p = file.GpsDecimal.Split(',');
            lat = p.Length > 0 ? p[0].Trim() : "";
            lon = p.Length > 1 ? p[1].Trim() : "";
        }
        
        GpsData.Add(new() { Key = "Latitud", Value = lat, Group = "GPS", IsEditable = true, GpsFormat = SelectedGpsFormat });
        GpsData.Add(new() { Key = "Longitud", Value = lon, Group = "GPS", IsEditable = true, GpsFormat = SelectedGpsFormat });

        string titulo = file.Name;
        string autor = "Desconocido";
        string copyright = "Desconocido";
        string software = "Desconocido";
        string calificacion = "0";
        string descripcion = "";
        string tags = "";

        string marca = "Desconocida";
        string modelo = "Desconocido";
        string lente = "Desconocido";
        string apertura = "Desconocida";
        string velocidad = "Desconocida";
        string iso = "Desconocido";
        string distanciaFocal = "Desconocida";
        string fechaCaptura = "Desconocida";
        string flash = "Desconocido";
        string modoExposicion = "Desconocido";
        string balanceBlancos = "Desconocido";
        string resolucion = file.Resolution;

        try 
        {
            using var img = new MagickImage(file.FullPath);
            var exif = img.GetExifProfile();
            if (exif != null)
            {
                var make = exif.GetValue(ExifTag.Make)?.Value?.ToString();
                if (!string.IsNullOrEmpty(make)) marca = make;

                var model = exif.GetValue(ExifTag.Model)?.Value?.ToString();
                if (!string.IsNullOrEmpty(model)) modelo = model;

                var dt = exif.GetValue(ExifTag.DateTimeOriginal)?.Value?.ToString();
                if (!string.IsNullOrEmpty(dt)) fechaCaptura = dt;

                var lens = exif.GetValue(ExifTag.LensModel)?.Value?.ToString();
                if (!string.IsNullOrEmpty(lens)) lente = lens;

                var isoVal = exif.GetValue(ExifTag.ISOSpeedRatings)?.Value?.ToString();
                if (!string.IsNullOrEmpty(isoVal)) iso = isoVal;

                var softwareVal = exif.GetValue(ExifTag.Software)?.Value?.ToString();
                if (!string.IsNullOrEmpty(softwareVal)) software = softwareVal;

                // Aperture (FNumber)
                var fNumber = exif.GetValue(ExifTag.FNumber)?.Value;
                if (fNumber.HasValue)
                {
                    var fn = fNumber.Value;
                    double fVal = (double)fn.Numerator / fn.Denominator;
                    apertura = $"f/{fVal:F1}";
                }

                // Shutter Speed (ExposureTime)
                var exposureTime = exif.GetValue(ExifTag.ExposureTime)?.Value;
                if (exposureTime.HasValue)
                {
                    var et = exposureTime.Value;
                    if (et.Numerator < et.Denominator)
                        velocidad = $"{et.Numerator}/{et.Denominator} s";
                    else
                    {
                        double secs = (double)et.Numerator / et.Denominator;
                        velocidad = $"{secs:F1} s";
                    }
                }

                // Focal Length
                var focalLength = exif.GetValue(ExifTag.FocalLength)?.Value;
                if (focalLength.HasValue)
                {
                    var fl = focalLength.Value;
                    double flVal = (double)fl.Numerator / fl.Denominator;
                    distanciaFocal = $"{flVal:F0} mm";
                }

                // Flash
                var flashVal = exif.GetValue(ExifTag.Flash)?.Value;
                if (flashVal != null)
                {
                    ushort fv = (ushort)flashVal;
                    flash = (fv & 0x01) == 1 ? "Disparado" : "No disparado";
                }

                // Exposure Program / Mode
                var expProgram = exif.GetValue(ExifTag.ExposureProgram)?.Value;
                if (expProgram != null)
                {
                    modoExposicion = (ushort)expProgram switch
                    {
                        1 => "Manual",
                        2 => "Auto",
                        3 => "Prioridad Apertura",
                        4 => "Prioridad Obturador",
                        _ => $"Programa ({expProgram})"
                    };
                }

                // White Balance
                var wb = exif.GetValue(ExifTag.WhiteBalance)?.Value;
                if (wb != null)
                {
                    balanceBlancos = (ushort)wb switch
                    {
                        0 => "Auto",
                        1 => "Manual",
                        _ => $"Otro ({wb})"
                    };
                }
            }

            var iptc = img.GetIptcProfile();
            if (iptc != null)
            {
                var titleVal = iptc.GetValue(IptcTag.Title)?.Value?.ToString();
                if (!string.IsNullOrEmpty(titleVal)) titulo = titleVal;

                var authorVal = iptc.GetValue(IptcTag.Byline)?.Value?.ToString();
                if (!string.IsNullOrEmpty(authorVal)) autor = authorVal;

                var copyrightVal = iptc.GetValue(IptcTag.CopyrightNotice)?.Value?.ToString();
                if (!string.IsNullOrEmpty(copyrightVal)) copyright = copyrightVal;

                var captionVal = iptc.GetValue(IptcTag.Caption)?.Value?.ToString();
                if (!string.IsNullOrEmpty(captionVal)) descripcion = captionVal;

                // Keywords (may have multiple)
                var keywords = iptc.GetAllValues(IptcTag.Keyword);
                if (keywords != null && keywords.Any())
                {
                    tags = string.Join(", ", keywords.Select(k => k.Value));
                }
            }
        } 
        catch { }

        ExifData.Add(new() { Key = "Cámara", Value = $"{marca} {modelo}".Trim(), Group = "EXIF", IsEditable = true });
        ExifData.Add(new() { Key = "Lente", Value = lente, Group = "EXIF", IsEditable = true });
        ExifData.Add(new() { Key = "Apertura", Value = apertura, Group = "EXIF", IsEditable = true });
        ExifData.Add(new() { Key = "Velocidad", Value = velocidad, Group = "EXIF", IsEditable = true });
        ExifData.Add(new() { Key = "ISO", Value = iso, Group = "EXIF", IsEditable = true });
        ExifData.Add(new() { Key = "Distancia Focal", Value = distanciaFocal, Group = "EXIF", IsEditable = true });
        ExifData.Add(new() { Key = "Fecha Captura", Value = fechaCaptura, Group = "EXIF", IsEditable = false });
        ExifData.Add(new() { Key = "Flash", Value = flash, Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "Disparado", "No disparado", "Auto" } });
        ExifData.Add(new() { Key = "Modo Exposición", Value = modoExposicion, Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "Manual", "Auto", "Prioridad Apertura", "Prioridad Obturador" } });
        ExifData.Add(new() { Key = "Balance Blancos", Value = balanceBlancos, Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "Auto", "Luz de día", "Nublado", "Sombra", "Tungsteno", "Fluorescente" } });
        ExifData.Add(new() { Key = "Resolución", Value = resolucion, Group = "EXIF", IsEditable = true });

        AuthorshipData.Add(new() { Key = "Autor", Value = autor, Group = "Autoría", IsEditable = true });
        AuthorshipData.Add(new() { Key = "Copyright", Value = copyright, Group = "Autoría", IsEditable = true });
        AuthorshipData.Add(new() { Key = "Software", Value = software, Group = "Autoría", IsEditable = true });
        AuthorshipData.Add(new() { Key = "Calificación", Value = calificacion, Group = "Autoría", IsEditable = true, EditorType = "Rating" });

        IptcData.Add(new() { Key = "Título", Value = titulo, Group = "IPTC/XMP", IsEditable = true });
        IptcData.Add(new() { Key = "Descripción", Value = descripcion, Group = "IPTC/XMP", IsEditable = true });
        IptcData.Add(new() { Key = "Palabras Clave", Value = tags, Group = "IPTC/XMP", IsEditable = true });
        
        // Populate editor fields for bindings
        EditAuthor = autor;
        EditCopyright = copyright;
        EditDescription = descripcion;
        EditSoftware = software;
        EditTags = tags;
        
        if (file.GpsDecimal != "—")
        {
            var p = file.GpsDecimal.Split(',');
            if (p.Length >= 2)
            {
                GpsLatitude = p[0].Trim();
                GpsLongitude = p[1].Trim();
            }
        }
        else
        {
            GpsLatitude = "";
            GpsLongitude = "";
        }
        GpsAltitude = "";

        foreach (var entry in GeneralInfo.Concat(GpsData).Concat(ExifData).Concat(IptcData).Concat(AuthorshipData))
        {
            entry.OriginalValue = entry.Value;
        }
    }

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

    private async Task ReloadFilesFromPathAsync(string path, string? targetFileToSelect = null)
    {
        var filesList = new ObservableCollection<FileItem>();
        
        await Task.Run(() =>
        {
            var fileInfos = SafeGetFiles(path).ToList();
            int count = 0;

            foreach (var fi in fileInfos)
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

                                    gpsDec = $"{latDec:F6}, {lonDec:F6}";
                                    
                                    string latDir = latDec >= 0 ? "N" : "S";
                                    string lonDir = lonDec >= 0 ? "E" : "W";
                                    gpsDMS = $"{latD}° {latM}' {latS:F2}\" {latDir}, {lonD}° {lonM}' {lonS:F2}\" {lonDir}";
                                }
                            }
                        }
                    }
                    catch { }
                }

                var item = new FileItem
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
        // Debounce para evitar recargas múltiples al guardar un archivo (FileSystemWatcher dispara varios eventos)
        if ((DateTime.Now - _lastReload).TotalMilliseconds < 500) return;
        _lastReload = DateTime.Now;

        Dispatcher.UIThread.InvokeAsync(() => 
        {
            _ = ReloadFilesFromPathAsync(CurrentPath);
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
                    var foundPhone = AvailablePhoneDevices.FirstOrDefault(d => string.Equals(d.Id, state.SelectedPhoneDeviceId, StringComparison.OrdinalIgnoreCase));
                    if (foundPhone != null) SelectedPhoneDevice = foundPhone;
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
