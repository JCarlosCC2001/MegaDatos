using System;

namespace MegaDatos.Models;

public class FileItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime DateModified { get; set; }
    public bool IsDirectory { get; set; }
    public string FileType { get; set; } = string.Empty;
    public string Resolution { get; set; } = "8256 × 5504 px";
    public string GpsCoordinates { get; set; } = "4.7110° N, 74.0721° W";

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
