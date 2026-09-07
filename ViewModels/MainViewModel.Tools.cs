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

}
