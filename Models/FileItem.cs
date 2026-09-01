using System;

using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDatos.Models;

public partial class FileItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsDirectory { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isCheckable = true;
    public bool IsCheckable
    {
        get => _isCheckable;
        set => SetProperty(ref _isCheckable, value);
    }
    public string FileType { get; set; } = string.Empty;
    public string Resolution { get; set; } = "8256 × 5504 px";
    public string GpsDecimal { get; set; } = "—";
    public string GpsDMS { get; set; } = "—";

    public string MegapixelsFormatted
    {
        get
        {
            if (string.IsNullOrEmpty(Resolution) || Resolution == "—") return "—";
            var parts = Resolution.Replace(" px", "").Split('×', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            {
                double mp = (w * h) / 1_000_000.0;
                return $"{mp:F1} MP";
            }
            return "—";
        }
    }

    public string SizeFormatted
    {
        get
        {
            if (IsDirectory) return "—";
            if (SizeBytes < 1024) return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024) return $"{SizeBytes / 1024.0:F1} KB";
            if (SizeBytes < 1024 * 1024 * 1024) return $"{SizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{SizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public string IconGlyph
    {
        get
        {
            if (IsDirectory) return "📁";
            return Extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".tiff" or ".bmp" or ".webp" or ".gif" => "🖼️",
                ".cr2" or ".nef" or ".arw" or ".dng" or ".raw" => "📷",
                ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" => "🎬",
                ".mp3" or ".flac" or ".wav" or ".aac" or ".ogg" => "🎵",
                ".pdf" => "📄",
                ".docx" or ".doc" => "📝",
                ".xlsx" or ".xls" => "📊",
                _ => "📎"
            };
        }
    }
}
