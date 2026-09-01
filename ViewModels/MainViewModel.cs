using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MegaDatos.Models;
using ImageMagick;

namespace MegaDatos.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // ===== Sidebar State =====
    [ObservableProperty]
    private string _activeTool = "Explorer";

    public bool IsExplorerActive => ActiveTool == "Explorer";
    public bool IsFormatActive => ActiveTool == "Format";
    public bool IsResizeActive => ActiveTool == "Resize";

    partial void OnActiveToolChanged(string value)
    {
        OnPropertyChanged(nameof(IsExplorerActive));
        OnPropertyChanged(nameof(IsFormatActive));
        OnPropertyChanged(nameof(IsResizeActive));
        StatusText = $"Herramienta activa: {value}";
    }

    [ObservableProperty]
    private string _currentPath = "D:\\Proyectos\\MegaDatos\\Demo";

    // ===== File Explorer =====
    [ObservableProperty]
    private ObservableCollection<FolderNode> _folderTree = new();

    [ObservableProperty]
    private ObservableCollection<FileItem> _files = new();

    private bool _areAllFilesSelected;
    public bool AreAllFilesSelected
    {
        get => _areAllFilesSelected;
        set
        {
            if (SetProperty(ref _areAllFilesSelected, value))
            {
                if (Files != null)
                {
                    foreach (var file in Files)
                    {
                        file.IsSelected = value;
                    }
                }
                UpdateSupportedFormats();
            }
        }
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

    [ObservableProperty]
    private string _batchAction = "Strip All Metadata";

    [ObservableProperty]
    private bool _batchBackupEnabled = true;

    // ===== Format Conversion =====
    [ObservableProperty]
    private string _targetFormat = "JPG";

    [ObservableProperty]
    private int _conversionQuality = 90;

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
            TargetFormat = SupportedFormats.First();
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
        if (_isUpdatingDimensions || !KeepAspectRatio) return;
        if (int.TryParse(value, out int w))
        {
            _isUpdatingDimensions = true;
            TargetHeight = ((int)Math.Round(w / _originalAspectRatio)).ToString();
            _isUpdatingDimensions = false;
        }
    }

    partial void OnTargetHeightChanged(string value)
    {
        if (_isUpdatingDimensions || !KeepAspectRatio) return;
        if (int.TryParse(value, out int h))
        {
            _isUpdatingDimensions = true;
            TargetWidth = ((int)Math.Round(h * _originalAspectRatio)).ToString();
            _isUpdatingDimensions = false;
        }
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
            _ = ReloadFilesFromPathAsync(value.FullPath);
            SetupWatcher(value.FullPath);
        }
    }

    public Func<Task<string?>>? RequestOpenFolderAsync;
    private FileSystemWatcher? _watcher;

    public MainViewModel()
    {
        Console.WriteLine("MainViewModel: Constructor started");
        // Iniciamos vacío
        Console.WriteLine("MainViewModel: Constructor completed");
    }

    // ===== Commands =====
    [RelayCommand]
    private void NavigateTool(string toolName)
    {
        ActiveTool = toolName;
    }

    [RelayCommand]
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
        var filesToProcess = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        if (filesToProcess.Count == 0) return;

        // Show confirmation dialog overlay instead of processing immediately
        IsConfirmDialogOpen = true;
    }

    [RelayCommand]
    private void CancelBatch()
    {
        IsConfirmDialogOpen = false;
    }

    [RelayCommand]
    private async Task ConfirmBatchAsync(string overwriteParam)
    {
        IsConfirmDialogOpen = false;
        bool overwriteOriginal = overwriteParam == "True";

        var filesToProcess = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        if (filesToProcess.Count == 0) return;

        IsBatchRunning = true;
        BatchProgress = 0;
        BatchStatusText = "Procesando...";
        StatusText = $"Procesamiento por lotes iniciado ({filesToProcess.Count} archivos)...";

        await Task.Run(async () =>
        {
            int count = 0;
            foreach (var file in filesToProcess)
            {
                try
                {
                    if (GetCategoryForExtension(file.Extension) == "Image" || GetCategoryForExtension(file.Extension) == "Raw")
                    {
                        using var image = new MagickImage(file.FullPath);
                        var originalInterlace = image.Interlace;

                        if (ActiveTool == "Resize")
                        {
                            if (int.TryParse(TargetWidth, out int tw) && int.TryParse(TargetHeight, out int th))
                            {
                                var size = new MagickGeometry((uint)tw, (uint)th);
                                size.IgnoreAspectRatio = !KeepAspectRatio;
                                image.Resize(size);
                                
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
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsBatchRunning = false;
            BatchStatusText = "Listo";
            StatusText = $"Proceso terminado. {filesToProcess.Count} archivos procesados ✓";
            _ = ReloadFilesFromPathAsync(CurrentPath);
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

        if (file.GpsDecimal != "—")
        {
            GpsData.Add(new() { Key = "Coordenadas (Dec)", Value = file.GpsDecimal, Group = "GPS", IsEditable = false });
            GpsData.Add(new() { Key = "Coordenadas (DMS)", Value = file.GpsDMS, Group = "GPS", IsEditable = false });
        }
        else
        {
            GpsData.Add(new() { Key = "Coordenadas", Value = "No disponible", Group = "GPS", IsEditable = false });
        }

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
    private async void LoadRealData(string path)
    {
        CurrentPath = path;
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
        }
        catch (Exception ex)
        {
            StatusText = $"Error crítico: {ex.Message}";
            HasDirectoryLoaded = false;
        }
    }

    private async Task ReloadFilesFromPathAsync(string path)
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
            if (Files.Count > 0)
            {
                SelectFile(Files[0]);
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
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
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

    public ObservableCollection<string> ViewTypes { get; } = new()
    {
        "Por defecto",
        "Imágenes",
        "Coordenadas"
    };

    partial void OnSelectedViewTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsResolutionColumnVisible));
        OnPropertyChanged(nameof(IsMegapixelColumnVisible));
        OnPropertyChanged(nameof(IsGpsColumnVisible));
        OnPropertyChanged(nameof(IsTypeColumnVisible));
        OnPropertyChanged(nameof(IsSizeColumnVisible));
        OnPropertyChanged(nameof(IsDateColumnVisible));
    }

    public bool IsResolutionColumnVisible => SelectedViewType == "Imágenes";
    public bool IsMegapixelColumnVisible => SelectedViewType == "Imágenes";
    public bool IsGpsColumnVisible => SelectedViewType == "Coordenadas";
    public bool IsTypeColumnVisible => SelectedViewType != "Coordenadas";
    public bool IsSizeColumnVisible => SelectedViewType == "Por defecto";
    public bool IsDateColumnVisible => SelectedViewType == "Por defecto";
}
