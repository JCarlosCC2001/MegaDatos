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

}
