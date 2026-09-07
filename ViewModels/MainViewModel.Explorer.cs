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

}
