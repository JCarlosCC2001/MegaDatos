using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDatos.Models;

public partial class DuplicateItem : ObservableObject
{
    public FileItem File { get; }

    [ObservableProperty]
    private bool _isSelectedForDeletion;

    [ObservableProperty]
    private bool _isBestQuality;

    public string Resolution => File.Resolution;
    public string SizeFormatted => File.SizeFormatted;
    public string Name => File.Name;
    public string Extension => File.Extension;
    public string FullPath => File.FullPath;

    public DuplicateItem(FileItem file)
    {
        File = file;
    }
}

public partial class DuplicateGroup : ObservableObject
{
    public string GroupId { get; }

    public ObservableCollection<DuplicateItem> Items { get; } = new();

    public string GroupTitle => $"Grupo de duplicados ({Items.Count} imágenes)";

    public DuplicateGroup(string groupId, IEnumerable<FileItem> files)
    {
        GroupId = groupId;
        foreach (var f in files)
        {
            Items.Add(new DuplicateItem(f));
        }
        EvaluateBestQuality();
    }

    public void EvaluateBestQuality()
    {
        if (Items.Count == 0) return;

        // Reset all
        foreach (var item in Items)
        {
            item.IsBestQuality = false;
            item.IsSelectedForDeletion = false;
        }

        // Determine best based on:
        // 1. Resolution (Width * Height)
        // 2. File size (SizeBytes)
        var bestItem = Items.OrderByDescending(i => GetTotalPixels(i.File.Resolution))
                            .ThenByDescending(i => i.File.SizeBytes)
                            .First();

        bestItem.IsBestQuality = true;
        bestItem.IsSelectedForDeletion = false; // Always keep the best

        // Mark the rest for deletion
        foreach (var item in Items)
        {
            if (item != bestItem)
            {
                item.IsSelectedForDeletion = true;
            }
        }
    }

    private long GetTotalPixels(string resolution)
    {
        if (string.IsNullOrEmpty(resolution) || resolution == "—") return 0;
        var parts = resolution.Replace(" px", "").Split('×', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
        {
            return (long)w * h;
        }
        return 0;
    }
}
