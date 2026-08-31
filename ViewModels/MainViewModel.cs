using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDatos.Models;

namespace MegaDatos.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // ===== Sidebar State =====
    [ObservableProperty]
    private string _activeTool = "Explorer";

    public bool IsExplorerActive => ActiveTool == "Explorer";
    public bool IsEditorActive => ActiveTool == "Editor";
    public bool IsGpsActive => ActiveTool == "GPS";
    public bool IsBatchActive => ActiveTool == "Batch";
    public bool IsStripActive => ActiveTool == "Strip";

    partial void OnActiveToolChanged(string value)
    {
        OnPropertyChanged(nameof(IsExplorerActive));
        OnPropertyChanged(nameof(IsEditorActive));
        OnPropertyChanged(nameof(IsGpsActive));
        OnPropertyChanged(nameof(IsBatchActive));
        OnPropertyChanged(nameof(IsStripActive));
        StatusText = $"Herramienta activa: {value}";
    }

    [ObservableProperty]
    private string _currentPath = "D:\\Proyectos\\MegaDatos\\Demo";

    // ===== File Explorer =====
    [ObservableProperty]
    private ObservableCollection<FolderNode> _folderTree = new();

    [ObservableProperty]
    private ObservableCollection<FileItem> _files = new();

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

    // ===== Batch Operation =====
    [ObservableProperty]
    private string _batchAction = "Strip All Metadata";

    [ObservableProperty]
    private bool _batchBackupEnabled = true;

    [ObservableProperty]
    private bool _batchRecursive;

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

    public MainViewModel()
    {
        Console.WriteLine("MainViewModel: Constructor started");
        LoadDemoData();
        Console.WriteLine("MainViewModel: Constructor completed");
    }

    // ===== Commands =====
    [RelayCommand]
    private void NavigateTool(string toolName)
    {
        ActiveTool = toolName;
    }

    [RelayCommand]
    private void OpenFolder()
    {
        // Will be implemented with IDialogService
        StatusText = "Abrir carpeta...";
    }

    [RelayCommand]
    private void StripMetadata()
    {
        StatusText = "Saneamiento iniciado...";
    }

    [RelayCommand]
    private void SaveMetadata()
    {
        IsBackupDialogOpen = true;
    }

    [RelayCommand]
    private void ConfirmSave(string withBackupStr)
    {
        bool withBackup = bool.Parse(withBackupStr);
        IsBackupDialogOpen = false;
        if (withBackup)
        {
            StatusText = "Metadatos guardados (Copia de seguridad .bak creada) ✓";
        }
        else
        {
            StatusText = "Metadatos guardados (Sin copia de seguridad) ✓";
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
        IsBatchRunning = true;
        BatchProgress = 0;
        BatchStatusText = "Procesando...";
        StatusText = "Procesamiento por lotes en curso...";
    }

    [RelayCommand]
    private void RefreshFiles()
    {
        StatusText = "Actualizando...";
    }

    [RelayCommand]
    private void SelectFile(FileItem? file)
    {
        if (file == null) return;
        SelectedFile = file;
        HasFileSelected = true;
        SelectedFileName = file.Name;
        SelectedCount = 1;
        LoadDemoMetadata(file);
    }

    // ===== Demo Data =====
    private void LoadDemoData()
    {
        // Demo folder tree
        var documents = new FolderNode
        {
            Name = "📁 Documentos",
            FullPath = "D:\\Documentos",
            Children = new ObservableCollection<FolderNode>
            {
                new() { Name = "📁 Proyectos", FullPath = "D:\\Documentos\\Proyectos" },
                new() { Name = "📁 Reportes", FullPath = "D:\\Documentos\\Reportes" },
            }
        };

        var photos = new FolderNode
        {
            Name = "📁 Fotografías",
            FullPath = "D:\\Fotografías",
            IsExpanded = true,
            Children = new ObservableCollection<FolderNode>
            {
                new() { Name = "📁 Sesión 2026-08", FullPath = "D:\\Fotografías\\Sesión 2026-08",
                    Children = new ObservableCollection<FolderNode>
                    {
                        new() { Name = "📁 RAW", FullPath = "D:\\Fotografías\\Sesión 2026-08\\RAW" },
                        new() { Name = "📁 Editadas", FullPath = "D:\\Fotografías\\Sesión 2026-08\\Editadas" },
                    }
                },
                new() { Name = "📁 Eventos", FullPath = "D:\\Fotografías\\Eventos" },
                new() { Name = "📁 Portfolio", FullPath = "D:\\Fotografías\\Portfolio" },
            }
        };

        var media = new FolderNode
        {
            Name = "📁 Media",
            FullPath = "D:\\Media",
            Children = new ObservableCollection<FolderNode>
            {
                new() { Name = "📁 Videos", FullPath = "D:\\Media\\Videos" },
                new() { Name = "📁 Audio", FullPath = "D:\\Media\\Audio" },
            }
        };

        FolderTree = new ObservableCollection<FolderNode> { documents, photos, media };

        // Demo files
        Files = new ObservableCollection<FileItem>
        {
            new() { Name = "DSC_0421.jpg", Extension = ".jpg", SizeBytes = 8_540_000, DateModified = new DateTime(2026, 8, 15, 14, 23, 0), FileType = "JPEG Image", Resolution = "8256 × 5504 px", GpsCoordinates = "4.7110° N, 74.0721° W" },
            new() { Name = "DSC_0422.jpg", Extension = ".jpg", SizeBytes = 9_120_000, DateModified = new DateTime(2026, 8, 15, 14, 24, 0), FileType = "JPEG Image", Resolution = "8256 × 5504 px", GpsCoordinates = "4.7115° N, 74.0718° W" },
            new() { Name = "DSC_0423.CR2", Extension = ".CR2", SizeBytes = 32_450_000, DateModified = new DateTime(2026, 8, 15, 14, 25, 0), FileType = "Canon RAW", Resolution = "5616 × 3744 px", GpsCoordinates = "4.7122° N, 74.0705° W" },
            new() { Name = "panorama_final.tiff", Extension = ".tiff", SizeBytes = 156_800_000, DateModified = new DateTime(2026, 8, 16, 9, 10, 0), FileType = "TIFF Image", Resolution = "12500 × 4200 px", GpsCoordinates = "4.7101° N, 74.0735° W" },
            new() { Name = "drone_clip_01.mp4", Extension = ".mp4", SizeBytes = 245_000_000, DateModified = new DateTime(2026, 8, 17, 11, 30, 0), FileType = "MP4 Video", Resolution = "3840 × 2160 px", GpsCoordinates = "4.7089° N, 74.0750° W" },
            new() { Name = "entrevista_audio.mp3", Extension = ".mp3", SizeBytes = 12_300_000, DateModified = new DateTime(2026, 8, 18, 16, 0, 0), FileType = "MP3 Audio", Resolution = "—", GpsCoordinates = "—" },
            new() { Name = "background_score.flac", Extension = ".flac", SizeBytes = 48_500_000, DateModified = new DateTime(2026, 8, 19, 8, 45, 0), FileType = "FLAC Audio", Resolution = "—", GpsCoordinates = "—" },
            new() { Name = "reporte_proyecto.pdf", Extension = ".pdf", SizeBytes = 2_340_000, DateModified = new DateTime(2026, 8, 20, 10, 0, 0), FileType = "PDF Document", Resolution = "—", GpsCoordinates = "—" },
            new() { Name = "retrato_estudio.png", Extension = ".png", SizeBytes = 5_670_000, DateModified = new DateTime(2026, 8, 21, 15, 30, 0), FileType = "PNG Image", Resolution = "3000 × 2000 px", GpsCoordinates = "—" },
            new() { Name = "timelapse_sunset.mov", Extension = ".mov", SizeBytes = 890_000_000, DateModified = new DateTime(2026, 8, 22, 19, 15, 0), FileType = "MOV Video", Resolution = "1920 × 1080 px", GpsCoordinates = "4.7130° N, 74.0690° W" },
            new() { Name = "product_shot_01.webp", Extension = ".webp", SizeBytes = 1_230_000, DateModified = new DateTime(2026, 8, 23, 12, 0, 0), FileType = "WebP Image", Resolution = "1200 × 800 px", GpsCoordinates = "—" },
            new() { Name = "DSC_0424.NEF", Extension = ".NEF", SizeBytes = 28_900_000, DateModified = new DateTime(2026, 8, 23, 14, 30, 0), FileType = "Nikon RAW", Resolution = "6048 × 4024 px", GpsCoordinates = "4.7112° N, 74.0720° W" },
        };

        TotalFiles = Files.Count;
        SelectedCount = 0;

        // Select first file
        SelectedFile = Files[0];
        HasFileSelected = true;
        SelectedFileName = Files[0].Name;
        SelectedCount = 1;
        LoadDemoMetadata(Files[0]);
    }

    private void LoadDemoMetadata(FileItem file)
    {
        GeneralInfo = new ObservableCollection<MetadataEntry>
        {
            new() { Key = "Nombre", Value = file.Name, Group = "General", IsEditable = true },
            new() { Key = "Ruta", Value = $"D:\\Fotografías\\Sesión 2026-08\\{file.Name}", Group = "General", IsEditable = true },
            new() { Key = "Tamaño", Value = file.SizeFormatted, Group = "General", IsEditable = true },
            new() { Key = "Tipo", Value = file.FileType, Group = "General", IsEditable = true },
            new() { Key = "Modificado", Value = file.DateModified.ToString("dd/MM/yyyy HH:mm"), Group = "General", IsEditable = true, EditorType = "Date" },
            new() { Key = "Creado", Value = file.DateModified.AddDays(-2).ToString("dd/MM/yyyy HH:mm"), Group = "General", IsEditable = true, EditorType = "Date" },
        };

        ExifData = new ObservableCollection<MetadataEntry>
        {
            new() { Key = "Cámara", Value = "Nikon Z8", Group = "EXIF", IsEditable = true },
            new() { Key = "Lente", Value = "NIKKOR Z 24-70mm f/2.8 S", Group = "EXIF", IsEditable = true },
            new() { Key = "Apertura", Value = "f/2.8", Group = "EXIF", IsEditable = true },
            new() { Key = "Velocidad", Value = "1/250 s", Group = "EXIF", IsEditable = true },
            new() { Key = "ISO", Value = "400", Group = "EXIF", IsEditable = true },
            new() { Key = "Distancia Focal", Value = "50 mm", Group = "EXIF", IsEditable = true },
            new() { Key = "Flash", Value = "No disparado", Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "Disparado", "No disparado", "Auto" } },
            new() { Key = "Modo Exposición", Value = "Manual", Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "Manual", "Auto", "Prioridad Apertura", "Prioridad Obturador" } },
            new() { Key = "Balance Blancos", Value = "Auto", Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "Auto", "Luz de día", "Nublado", "Sombra", "Tungsteno", "Fluorescente" } },
            new() { Key = "Resolución", Value = "8256 × 5504 px", Group = "EXIF", IsEditable = true },
            new() { Key = "Profundidad Color", Value = "14 bits", Group = "EXIF", IsEditable = true },
            new() { Key = "Espacio Color", Value = "sRGB", Group = "EXIF", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "sRGB", "Adobe RGB", "ProPhoto RGB" } },
        };

        AuthorshipData = new ObservableCollection<MetadataEntry>
        {
            new() { Key = "Autor", Value = "Juan Pérez Fotografía", Group = "Autoría", IsEditable = true },
            new() { Key = "Copyright", Value = "© 2026 Juan Pérez. Todos los derechos reservados.", Group = "Autoría", IsEditable = true },
            new() { Key = "Software", Value = "Adobe Lightroom Classic 14.2", Group = "Autoría", IsEditable = true },
            new() { Key = "Calificación", Value = "★★★★☆", Group = "Autoría", IsEditable = true, EditorType = "Rating" },
        };

        GpsData = new ObservableCollection<MetadataEntry>
        {
            new() { Key = "Latitud", Value = "4.7110° N", Group = "GPS", IsEditable = true },
            new() { Key = "Longitud", Value = "-74.0721° W", Group = "GPS", IsEditable = true },
            new() { Key = "Altitud", Value = "2,640 m", Group = "GPS", IsEditable = true },
            new() { Key = "Referencia", Value = "WGS-84", Group = "GPS", IsEditable = true, EditorType = "Choice", Choices = new List<string> { "WGS-84", "NAD-83", "ED-50" } },
        };

        foreach (var entry in GpsData)
        {
            if (entry.IsGpsCoordinate)
            {
                entry.GpsFormat = SelectedGpsFormat;
            }
        }

        IptcData = new ObservableCollection<MetadataEntry>
        {
            new() { Key = "Título", Value = "Atardecer en Bogotá", Group = "IPTC/XMP", IsEditable = true },
            new() { Key = "Descripción", Value = "Vista panorámica del atardecer desde Monserrate", Group = "IPTC/XMP", IsEditable = true },
            new() { Key = "Palabras Clave", Value = "paisaje, atardecer, bogotá, colombia, montaña", Group = "IPTC/XMP", IsEditable = true },
            new() { Key = "Categoría", Value = "Paisaje", Group = "IPTC/XMP", IsEditable = true },
            new() { Key = "Ciudad", Value = "Bogotá", Group = "IPTC/XMP", IsEditable = true },
            new() { Key = "País", Value = "Colombia", Group = "IPTC/XMP", IsEditable = true },
        };

        // Populate editor fields
        EditAuthor = "Juan Pérez Fotografía";
        EditCopyright = "© 2026 Juan Pérez. Todos los derechos reservados.";
        EditDescription = "Vista panorámica del atardecer desde Monserrate";
        EditSoftware = "Adobe Lightroom Classic 14.2";
        EditTags = "paisaje, atardecer, bogotá, colombia, montaña";
        GpsLatitude = "4.7110";
        GpsLongitude = "-74.0721";
        GpsAltitude = "2640";
    }

    [ObservableProperty]
    private string _selectedGpsFormat = "Decimal";

    partial void OnSelectedGpsFormatChanged(string value)
    {
        ConvertGpsDataFormat(value);
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
    private string _selectedViewType = "Metadatos Completos (Por defecto)";

    public ObservableCollection<string> ViewTypes { get; } = new()
    {
        "Metadatos Completos (Por defecto)",
        "Básica (Nombre, Tipo, Peso)",
        "Geolocalización (Coordenadas)",
        "Imágenes (Dimensiones)"
    };

    partial void OnSelectedViewTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsResolutionColumnVisible));
        OnPropertyChanged(nameof(IsGpsColumnVisible));
        OnPropertyChanged(nameof(IsTypeColumnVisible));
        OnPropertyChanged(nameof(IsSizeColumnVisible));
        OnPropertyChanged(nameof(IsDateColumnVisible));
    }

    public bool IsResolutionColumnVisible => SelectedViewType == "Metadatos Completos (Por defecto)" || SelectedViewType == "Imágenes (Dimensiones)";
    public bool IsGpsColumnVisible => SelectedViewType == "Metadatos Completos (Por defecto)" || SelectedViewType == "Geolocalización (Coordenadas)";
    public bool IsTypeColumnVisible => SelectedViewType != "Geolocalización (Coordenadas)";
    public bool IsSizeColumnVisible => SelectedViewType != "Geolocalización (Coordenadas)";
    public bool IsDateColumnVisible => SelectedViewType == "Metadatos Completos (Por defecto)" || SelectedViewType == "Básica (Nombre, Tipo, Peso)";
}
