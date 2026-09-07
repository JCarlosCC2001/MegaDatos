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

}
