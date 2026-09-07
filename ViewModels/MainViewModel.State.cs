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
        IsProcessing = true;

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
                IsProcessing = false;
            });
        });
    }

    [RelayCommand]
    private void ReevaluateTemplates()
    {
        EvaluateAllFilesCompliance();
        StatusText = $"Verificación de plantilla '{SelectedTemplate?.Name}' actualizada ✓";
    }

}
