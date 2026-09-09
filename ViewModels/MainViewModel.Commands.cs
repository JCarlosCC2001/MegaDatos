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
    // ===== Commands =====
    [RelayCommand]
    private void NavigateTool(string toolName)
    {
        ActiveTool = toolName;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
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

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task GenerateKmz()
    {
        var selectedFilesWithGps = Files?.Where(f => f.IsSelected && !string.IsNullOrEmpty(f.GpsDecimal) && f.GpsDecimal != "—").ToList();

        if (selectedFilesWithGps == null || selectedFilesWithGps.Count == 0)
        {
            StatusText = "No hay archivos seleccionados con coordenadas GPS válidas.";
            return;
        }

        if (RequestSaveKmzAsync == null) return;

        double refLat = 0, refLon = 0;
        bool hasValidRef = false;
        
        if (KmzEnableComparison)
        {
            var parsed = GpsGeneratorService.ParseCoordinates($"{GpsRefLat}, {GpsRefLon}");
            if (parsed.HasValue)
            {
                refLat = parsed.Value.Lat;
                refLon = parsed.Value.Lon;
                hasValidRef = true;
                
                double minDist = double.MaxValue;
                double maxDist = double.MinValue;
                double sumDist = 0;
                int count = 0;
                
                foreach(var file in selectedFilesWithGps)
                {
                    var fileGps = GpsGeneratorService.ParseCoordinates(file.GpsDecimal);
                    if(fileGps.HasValue)
                    {
                        double dist = GpsGeneratorService.CalculateHaversineDistance(refLat, refLon, fileGps.Value.Lat, fileGps.Value.Lon);
                        if (dist < minDist) minDist = dist;
                        if (dist > maxDist) maxDist = dist;
                        sumDist += dist;
                        count++;
                    }
                }
                
                if (count > 0)
                {
                    double avgDist = sumDist / count;
                    KmzMinDist = minDist < 1000 ? $"{Math.Round(minDist, 1)} m" : $"{Math.Round(minDist / 1000.0, 2)} km";
                    KmzMaxDist = maxDist < 1000 ? $"{Math.Round(maxDist, 1)} m" : $"{Math.Round(maxDist / 1000.0, 2)} km";
                    KmzAvgDist = avgDist < 1000 ? $"{Math.Round(avgDist, 1)} m" : $"{Math.Round(avgDist / 1000.0, 2)} km";
                    KmzStatsVisible = true;
                }
                else
                {
                    KmzStatsVisible = false;
                }
            }
            else
            {
                StatusText = "La coordenada de referencia no es válida.";
                return;
            }
        }
        else
        {
            KmzStatsVisible = false;
        }

        var savePath = await RequestSaveKmzAsync();
        if (string.IsNullOrEmpty(savePath)) return;

        IsProcessing = true;
        try
        {
            StatusText = $"Generando KMZ con {selectedFilesWithGps.Count} ubicaciones...";

            string kmlContent = GenerateKmlContent(selectedFilesWithGps, hasValidRef, refLat, refLon);

            using (var fs = new FileStream(savePath, FileMode.Create))
            using (var archive = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                var kmlEntry = archive.CreateEntry("doc.kml");
                using (var entryStream = kmlEntry.Open())
                using (var writer = new StreamWriter(entryStream, System.Text.Encoding.UTF8))
                {
                    await writer.WriteAsync(kmlContent);
                }
            }

            StatusText = $"KMZ guardado exitosamente en: {Path.GetFileName(savePath)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Error al generar KMZ: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private string GetRefSymbolUrl()
    {
        return KmzRefSymbol switch
        {
            "Estrella" => "http://maps.google.com/mapfiles/kml/shapes/star.png",
            "Círculo" => "http://maps.google.com/mapfiles/kml/shapes/placemark_circle.png",
            "Chincheta" => "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png",
            "Cuadrado" => "http://maps.google.com/mapfiles/kml/shapes/placemark_square.png",
            "Triángulo" => "http://maps.google.com/mapfiles/kml/shapes/triangle.png",
            _ => "http://maps.google.com/mapfiles/kml/shapes/star.png"
        };
    }

    private string GenerateKmlContent(List<FileItem> files, bool hasRef = false, double refLat = 0, double refLon = 0)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
        sb.AppendLine("  <Document>");
        sb.AppendLine("    <name>Exportación KMZ MegaDatos</name>");

        string kmlColor = KmzMarkerColor switch
        {
            "Rojo" => "ff0000ff",
            "Azul" => "ffff0000",
            "Verde" => "ff00ff00",
            "Amarillo" => "ff00ffff",
            "Blanco" => "ffffffff",
            _ => "ff0000ff"
        };

        string iconUrl = "http://maps.google.com/mapfiles/kml/pushpin/ylw-pushpin.png";

        sb.AppendLine("    <Style id=\"customStyle\">");
        sb.AppendLine("      <IconStyle>");
        sb.AppendLine($"        <color>{kmlColor}</color>");
        sb.AppendLine("        <scale>1.0</scale>");
        sb.AppendLine("        <Icon>");
        sb.AppendLine($"          <href>{iconUrl}</href>");
        sb.AppendLine("        </Icon>");
        sb.AppendLine("      </IconStyle>");
        sb.AppendLine("      <LineStyle>");
        sb.AppendLine($"        <color>{kmlColor}</color>");
        sb.AppendLine("        <width>3</width>");
        sb.AppendLine("      </LineStyle>");
        sb.AppendLine("    </Style>");

        if (hasRef && KmzEnableComparison)
        {
            if (KmzIncludeRefPoint)
            {
                sb.AppendLine("    <Placemark>");
                sb.AppendLine("      <name>Coordenada de Referencia</name>");
                sb.AppendLine("      <Style>");
                sb.AppendLine("        <IconStyle>");
                sb.AppendLine("          <color>ff00ffff</color>"); // Yellow
                sb.AppendLine("          <scale>1.5</scale>");
                sb.AppendLine("          <Icon>");
                sb.AppendLine($"            <href>{GetRefSymbolUrl()}</href>");
                sb.AppendLine("          </Icon>");
                sb.AppendLine("        </IconStyle>");
                sb.AppendLine("      </Style>");
                sb.AppendLine("      <Point>");
                sb.AppendLine($"        <coordinates>{refLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{refLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}</coordinates>");
                sb.AppendLine("      </Point>");
                sb.AppendLine("    </Placemark>");
            }

            if (KmzDrawCircle && double.TryParse(KmzCircleRadius.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double radius) && radius > 0)
            {
                sb.AppendLine("    <Placemark>");
                sb.AppendLine($"      <name>Radio de {radius} m</name>");
                sb.AppendLine("      <Style>");
                sb.AppendLine("        <LineStyle>");
                sb.AppendLine("          <color>8800ffff</color>");
                sb.AppendLine("          <width>2</width>");
                sb.AppendLine("        </LineStyle>");
                sb.AppendLine("        <PolyStyle>");
                sb.AppendLine("          <color>3300ffff</color>");
                sb.AppendLine("        </PolyStyle>");
                sb.AppendLine("      </Style>");
                sb.AppendLine("      <Polygon>");
                sb.AppendLine("        <outerBoundaryIs>");
                sb.AppendLine("          <LinearRing>");
                sb.AppendLine("            <coordinates>");
                
                for (int i = 0; i <= 360; i += 10)
                {
                    double angle = i * (Math.PI / 180.0);
                    double deltaLat = (radius * Math.Cos(angle)) / 111139.0;
                    double centerLatRad = refLat * (Math.PI / 180.0);
                    double cosLat = Math.Cos(centerLatRad);
                    if (Math.Abs(cosLat) < 1e-6) cosLat = 1e-6;
                    double deltaLon = (radius * Math.Sin(angle)) / (111139.0 * cosLat);
                    
                    double newLat = Math.Clamp(refLat + deltaLat, -90.0, 90.0);
                    double newLon = Math.Clamp(refLon + deltaLon, -180.0, 180.0);
                    
                    sb.AppendLine($"              {newLon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{newLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                }
                
                sb.AppendLine("            </coordinates>");
                sb.AppendLine("          </LinearRing>");
                sb.AppendLine("        </outerBoundaryIs>");
                sb.AppendLine("      </Polygon>");
                sb.AppendLine("    </Placemark>");
            }
        }

        var sortedFiles = files.OrderBy(f => f.DateModified).ToList();
        var validCoordinates = new List<string>();

        foreach (var file in sortedFiles)
        {
            var parts = file.GpsDecimal.Split(',');
            if (parts.Length == 2 && 
                double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lon))
            {
                string coord = $"{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                validCoordinates.Add(coord);

                sb.AppendLine("    <Placemark>");
                
                string title = KmzIncludeFilename ? EscapeXml(file.Name) : "Marcador";
                sb.AppendLine($"      <name>{title}</name>");
                sb.AppendLine("      <styleUrl>#customStyle</styleUrl>");
                
                if (KmzIncludeDate)
                {
                    string dateStr = file.DateModified.ToString("yyyy-MM-dd HH:mm:ss");
                    sb.AppendLine($"      <description>Fecha: {dateStr}</description>");
                }
                
                sb.AppendLine("      <Point>");
                sb.AppendLine($"        <coordinates>{coord}</coordinates>");
                sb.AppendLine("      </Point>");
                sb.AppendLine("    </Placemark>");
            }
        }

        if (KmzIncludePath && validCoordinates.Count > 1)
        {
            sb.AppendLine("    <Placemark>");
            sb.AppendLine("      <name>Ruta (Trazo de Recorrido)</name>");
            sb.AppendLine("      <styleUrl>#customStyle</styleUrl>");
            sb.AppendLine("      <LineString>");
            sb.AppendLine("        <tessellate>1</tessellate>");
            sb.AppendLine("        <coordinates>");
            foreach (var coord in validCoordinates)
            {
                sb.AppendLine($"          {coord}");
            }
            sb.AppendLine("        </coordinates>");
            sb.AppendLine("      </LineString>");
            sb.AppendLine("    </Placemark>");
        }

        sb.AppendLine("  </Document>");
        sb.AppendLine("</kml>");
        
        return sb.ToString();
    }

    private string EscapeXml(string unescaped)
    {
        if (string.IsNullOrEmpty(unescaped)) return string.Empty;
        return unescaped.Replace("&", "&amp;")
                        .Replace("<", "&lt;")
                        .Replace(">", "&gt;")
                        .Replace("\"", "&quot;")
                        .Replace("'", "&apos;");
    }

    [RelayCommand]
    private void StripMetadata()
    {
        ActiveTool = "Clean";
        ExecuteBatch();
    }

    [RelayCommand]
    private void SaveMetadata()
    {
        IsBackupDialogOpen = true;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConfirmSave(object withBackupStr)
    {
        bool withBackup = withBackupStr?.ToString() == "True";
        IsBackupDialogOpen = false;

        if (SelectedFile == null) return;
        StatusText = "Guardando metadatos...";



        var args = new List<string>();
        if (!withBackup) args.Add("-overwrite_original");

        // Helper to map UI names to ExifTool tags
        string MapToExifTag(string group, string key)
        {
            if (group == "General")
            {
                if (key == "Nombre") return "FileName";
            }
            if (group == "GPS")
            {
                if (key == "Latitud") return "GPSLatitude";
                if (key == "Longitud") return "GPSLongitude";
                if (key == "Altitud") return "GPSAltitude";
            }
            if (group == "EXIF")
            {
                if (key == "Marca") return "Make";
                if (key == "Modelo") return "Model";
                if (key == "Lente") return "LensModel";
                if (key == "Apertura") return "FNumber";
                if (key == "Velocidad") return "ExposureTime";
                if (key == "ISO") return "ISO";
                if (key == "Distancia Focal") return "FocalLength";
                if (key == "Flash") return "Flash";
                if (key == "Modo Exposición") return "ExposureProgram";
                if (key == "Balance Blancos") return "WhiteBalance";
            }
            if (group == "Autoría")
            {
                if (key == "Autor") return "Artist"; // or Creator
                if (key == "Copyright") return "Copyright";
                if (key == "Software") return "Software";
                if (key == "Calificación") return "Rating";
            }
            if (group == "IPTC/XMP")
            {
                if (key == "Título") return "Title";
                if (key == "Descripción") return "Description";
                if (key == "Palabras Clave") return "Subject";
            }
            return "";
        }

        var allEntries = GeneralInfo.Concat(GpsData).Concat(ExifData).Concat(IptcData).Concat(AuthorshipData);

        bool changesMade = false;
        foreach (var entry in allEntries)
        {
            if (entry.IsEditable && entry.Value != entry.OriginalValue)
            {
                string tag = MapToExifTag(entry.Group, entry.Key);
                if (!string.IsNullOrEmpty(tag))
                {
                    string cleanVal = entry.Value;
                    if (entry.Key == "Calificación") cleanVal = entry.Value.Count(c => c == '★').ToString();
                    
                    if (entry.IsGpsCoordinate)
                    {
                        // Extract hemisphere from the MetadataEntry
                        string hem = entry.Hemisphere;
                        
                        // Get the absolute numeric value
                        string numericVal = cleanVal
                            .Replace("°", "").Replace("'", "").Replace("\"", "")
                            .Replace("N", "").Replace("S", "").Replace("E", "").Replace("W", "")
                            .Trim();
                        
                        // Send the coordinate value as positive (absolute)
                        if (double.TryParse(numericVal, System.Globalization.NumberStyles.Any, 
                            System.Globalization.CultureInfo.InvariantCulture, out double coordVal))
                        {
                            numericVal = Math.Abs(coordVal).ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                        
                        args.Add($"-{tag}={numericVal}");
                        
                        // Send the hemisphere reference tag
                        if (entry.Key == "Latitud")
                        {
                            string latRef = (hem == "S") ? "S" : "N";
                            args.Add($"-GPSLatitudeRef={latRef}");
                        }
                        else if (entry.Key == "Longitud")
                        {
                            string lonRef = (hem == "W") ? "W" : "E";
                            args.Add($"-GPSLongitudeRef={lonRef}");
                        }
                    }
                    else
                    {
                        args.Add($"-{tag}={cleanVal}");
                    }
                    
                    changesMade = true;
                }
            }
        }

        if (changesMade)
        {
            try
            {
                args.Add(SelectedFile.FullPath);

                var result = await ExifToolService.ExecuteAsync(args, maxRetries: 5);

                if (result.Success)
                {
                    StatusText = withBackup ? "Metadatos guardados (Copia de seguridad .bak creada) ✓" : "Metadatos guardados (Sin copia de seguridad) ✓";
                    
                    string selectedPath = SelectedFile.FullPath;
                    _ = ReloadFilesFromPathAsync(CurrentPath, selectedPath);
                }
                else
                {
                    string msg = string.IsNullOrWhiteSpace(result.Error) ? result.Output : result.Error;
                    StatusText = $"Error al guardar tras reintentos: {msg}";
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }
        else
        {
            StatusText = "No se detectaron campos válidos para guardar.";
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
        if (ActiveTool == "Templates" && (SelectedTemplate == null || SelectedTemplate.Id == "None"))
        {
            StatusText = "Selecciona una plantilla válida para procesar.";
            return;
        }

        var filesToProcess = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        if (filesToProcess.Count == 0 && SelectedFile != null && !SelectedFile.IsDirectory)
        {
            filesToProcess.Add(SelectedFile);
        }

        if (filesToProcess.Count == 0)
        {
            StatusText = "Selecciona al menos un archivo para procesar.";
            return;
        }

        // Show confirmation dialog overlay instead of processing immediately
        IsConfirmDialogOpen = true;
    }

    [RelayCommand]
    private void CancelBatch()
    {
        IsConfirmDialogOpen = false;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConfirmBatchAsync(object overwriteParam)
    {
        IsConfirmDialogOpen = false;
        bool overwriteOriginal = overwriteParam?.ToString() == "True";

        var filesToProcess = Files.Where(f => f.IsSelected && !f.IsDirectory).ToList();
        if (filesToProcess.Count == 0 && SelectedFile != null && !SelectedFile.IsDirectory)
        {
            filesToProcess.Add(SelectedFile);
        }
        if (filesToProcess.Count == 0) return;

        IsBatchRunning = true;
        BatchProgress = 0;
        BatchStatusText = "Procesando...";
        IsProcessing = true;
        StatusText = $"Procesamiento iniciado ({filesToProcess.Count} archivos)...";
        await Task.Run(async () =>
        {
            int count = 0;
            (double Lat, double Lon)? sharedGps = null;
            bool hasCriticalError = false;
            string criticalErrorMessage = "";

            foreach (var file in filesToProcess)
            {
                if (hasCriticalError) break;

                try
                {
                    if (GetCategoryForExtension(file.Extension) == "Image" || GetCategoryForExtension(file.Extension) == "Raw")
                    {
                        using var image = new MagickImage(file.FullPath);
                        var originalInterlace = image.Interlace;

                        if (ActiveTool == "Clean")
                        {
                            // Rotar físicamente los píxeles basados en la etiqueta EXIF de orientación
                            // ANTES de borrar los metadatos, para que la imagen no quede volteada.
                            image.AutoOrient();

                            if (CleanAllMetadata)
                            {
                                image.Strip();
                            }
                            else
                            {
                                if (CleanExif) image.RemoveProfile("exif");
                                if (CleanIptc) image.RemoveProfile("iptc");
                                if (CleanAuthorship)
                                {
                                    image.RemoveProfile("8bim");
                                    image.RemoveProfile("xmp");
                                }
                                if (CleanGps)
                                {
                                    var exif = image.GetExifProfile();
                                    if (exif != null)
                                    {
                                        exif.RemoveValue(ExifTag.GPSLatitude);
                                        exif.RemoveValue(ExifTag.GPSLatitudeRef);
                                        exif.RemoveValue(ExifTag.GPSLongitude);
                                        exif.RemoveValue(ExifTag.GPSLongitudeRef);
                                        exif.RemoveValue(ExifTag.GPSAltitude);
                                        exif.RemoveValue(ExifTag.GPSAltitudeRef);
                                        exif.RemoveValue(ExifTag.GPSVersionID);
                                        exif.RemoveValue(ExifTag.GPSTimestamp);
                                        exif.RemoveValue(ExifTag.GPSDateStamp);
                                        exif.RemoveValue(ExifTag.GPSImgDirection);
                                        exif.RemoveValue(ExifTag.GPSImgDirectionRef);
                                        exif.RemoveValue(ExifTag.GPSDestBearing);
                                        exif.RemoveValue(ExifTag.GPSDestBearingRef);
                                        exif.RemoveValue(ExifTag.GPSMapDatum);
                                        exif.RemoveValue(ExifTag.GPSProcessingMethod);
                                        image.SetProfile(exif);
                                    }
                                }
                            }

                            string newName = overwriteOriginal 
                                ? Path.GetFileName(file.FullPath)
                                : Path.GetFileNameWithoutExtension(file.FullPath) + "_limpio" + file.Extension;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                            if (overwriteOriginal && BatchBackupEnabled)
                            {
                                string backupPath = file.FullPath + ".bak";
                                try
                                {
                                    if (!File.Exists(backupPath))
                                    {
                                        File.Copy(file.FullPath, backupPath, true);
                                    }
                                }
                                catch { }
                            }

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);
                            if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                            {
                                if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                else image.Format = MagickFormat.Jpeg;
                            }

                            image.Write(newPath);

                            try
                            {
                                File.SetCreationTime(newPath, creationTime);
                                File.SetLastWriteTime(newPath, lastWriteTime);
                            }
                            catch { }
                        }
                        else if (ActiveTool == "Templates" && SelectedTemplate != null)
                        {
                            string newName = overwriteOriginal 
                                ? Path.GetFileName(file.FullPath)
                                : Path.GetFileNameWithoutExtension(file.FullPath) + $"_{SelectedTemplate.Id.ToLowerInvariant()}" + file.Extension;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                            if (overwriteOriginal && BatchBackupEnabled)
                            {
                                string backupPath = file.FullPath + ".bak";
                                try
                                {
                                    if (!File.Exists(backupPath))
                                    {
                                        File.Copy(file.FullPath, backupPath, true);
                                    }
                                }
                                catch { }
                            }

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);

                            if (SelectedTemplate.UsesExifTool)
                            {
                                if (!overwriteOriginal && file.FullPath != newPath)
                                {
                                    File.Copy(file.FullPath, newPath, true);
                                }
                                
                                // Free file handle so ExifTool can modify it
                                image.Dispose(); 
                                
                                await SelectedTemplate.ApplyWithExifToolAsync(newPath, file);
                            }
                            else
                            {
                                SelectedTemplate.Apply(image, file);
                                if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                                {
                                    if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                    else image.Format = MagickFormat.Jpeg;
                                }
                                image.Write(newPath);
                            }

                            try
                            {
                                File.SetCreationTime(newPath, creationTime);
                                File.SetLastWriteTime(newPath, lastWriteTime);
                            }
                            catch { }
                        }
                        else if (ActiveTool == "Phone" && SelectedPhoneDevice != null)
                        {
                            string newName = overwriteOriginal 
                                ? Path.GetFileName(file.FullPath)
                                : Path.GetFileNameWithoutExtension(file.FullPath) + $"_{SelectedPhoneDevice.Id}" + file.Extension;
                            string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                            if (overwriteOriginal && BatchBackupEnabled)
                            {
                                string backupPath = file.FullPath + ".bak";
                                try
                                {
                                    if (!File.Exists(backupPath))
                                    {
                                        File.Copy(file.FullPath, backupPath, true);
                                    }
                                }
                                catch { }
                            }

                            var creationTime = File.GetCreationTime(file.FullPath);
                            var lastWriteTime = File.GetLastWriteTime(file.FullPath);

                            // Si no vamos a sobreescribir el original, copiamos la imagen base al nuevo destino primero
                            if (!overwriteOriginal)
                            {
                                File.Copy(file.FullPath, newPath, true);
                            }

                            // Aplicamos ExifTool sobre el archivo de destino (newPath si es copia, file.FullPath si es original)
                            string targetToProcess = overwriteOriginal ? file.FullPath : newPath;
                            
                            // Cerramos MagickImage antes de lanzar ExifTool si comparten el mismo archivo
                            image.Dispose(); 

                            try
                            {
                                await SelectedPhoneDevice.ApplyWithExifToolAsync(targetToProcess, file, PhoneOverwriteAll, PhonePreserveGps);

                                try
                                {
                                    File.SetCreationTime(targetToProcess, creationTime);
                                    File.SetLastWriteTime(targetToProcess, lastWriteTime);
                                }
                                catch { }
                            }
                            catch (Exception ex)
                            {
                                hasCriticalError = true;
                                criticalErrorMessage = ex.Message;
                                break;
                            }
                        }
                        else if (ActiveTool == "GpsGen")
                        {
                            if (double.TryParse(GpsGenCenterLat.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double cLat) &&
                                double.TryParse(GpsGenCenterLon.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double cLon) &&
                                double.TryParse(GpsGenRadius.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double radiusVal))
                            {
                                double radiusMeters = GpsGenRadiusUnit.Contains("Kilómetro") ? radiusVal * 1000.0 : radiusVal;
                                double? alt = double.TryParse(GpsGenBaseAltitude.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedAlt) ? parsedAlt : null;

                                (double Lat, double Lon) targetCoord;
                                if (GpsGenUniquePerPhoto)
                                {
                                    targetCoord = GpsGeneratorService.GenerateRandomCoordinate(cLat, cLon, radiusMeters);
                                }
                                else
                                {
                                    if (!sharedGps.HasValue)
                                    {
                                        sharedGps = GpsGeneratorService.GenerateRandomCoordinate(cLat, cLon, radiusMeters);
                                    }
                                    targetCoord = sharedGps.Value;
                                }

                                double targetLat = targetCoord.Lat;
                                double targetLon = targetCoord.Lon;

                                double? finalAlt = alt.HasValue ? alt.Value + (Random.Shared.NextDouble() * 10.0 - 5.0) : null;

                                GpsGeneratorService.ApplyGpsToImage(image, targetLat, targetLon, finalAlt);

                                string newName = overwriteOriginal 
                                    ? Path.GetFileName(file.FullPath)
                                    : Path.GetFileNameWithoutExtension(file.FullPath) + "_gps" + file.Extension;
                                string newPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);

                                if (overwriteOriginal && BatchBackupEnabled)
                                {
                                    string backupPath = file.FullPath + ".bak";
                                    try
                                    {
                                        if (!File.Exists(backupPath))
                                        {
                                            File.Copy(file.FullPath, backupPath, true);
                                        }
                                    }
                                    catch { }
                                }

                                var creationTime = File.GetCreationTime(file.FullPath);
                                var lastWriteTime = File.GetLastWriteTime(file.FullPath);
                                if (image.Format == MagickFormat.Jpeg || image.Format == MagickFormat.Pjpeg)
                                {
                                    if (originalInterlace == Interlace.Jpeg) image.Format = MagickFormat.Pjpeg;
                                    else image.Format = MagickFormat.Jpeg;
                                }

                                image.Write(newPath);

                                try
                                {
                                    File.SetCreationTime(newPath, creationTime);
                                    File.SetLastWriteTime(newPath, lastWriteTime);
                                }
                                catch { }
                            }
                        }
                        else if (ActiveTool == "Resize")
                        {
                            if (int.TryParse(TargetWidth, out int tw) && int.TryParse(TargetHeight, out int th))
                            {
                                var size = new MagickGeometry((uint)tw, (uint)th);
                                size.IgnoreAspectRatio = !KeepAspectRatio;
                                image.Resize(size);
                                
                                // Sincronización forense: Actualizar dimensiones internas en el perfil EXIF
                                // Nota: Al modificar el perfil EXIF, Magick.NET lo re-serializa por defecto en Little-endian (Intel, II),
                                // perdiendo el ExifByteOrder original (Big-endian, MM).
                                // Si se prefiere mantener el ByteOrder original intacto, es mejor no modificar el perfil aquí.
                                /*
                                var exif = image.GetExifProfile();
                                if (exif != null)
                                {
                                    exif.SetValue(ExifTag.PixelXDimension, new Number((uint)image.Width));
                                    exif.SetValue(ExifTag.PixelYDimension, new Number((uint)image.Height));

                                    if (exif.GetValue(ExifTag.ImageWidth) != null)
                                    {
                                        exif.SetValue(ExifTag.ImageWidth, new Number((uint)image.Width));
                                    }
                                    if (exif.GetValue(ExifTag.ImageLength) != null)
                                    {
                                        exif.SetValue(ExifTag.ImageLength, new Number((uint)image.Height));
                                    }

                                    image.SetProfile(exif);
                                }
                                */

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

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsBatchRunning = false;
                BatchStatusText = "Listo";
                if (hasCriticalError)
                {
                    StatusText = "❌ Error crítico: " + criticalErrorMessage;
                }
                else
                {
                    StatusText = $"Proceso terminado. {filesToProcess.Count} archivos procesados ✓";
                }
                _ = ReloadFilesFromPathAsync(CurrentPath);
            });
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

        SaveCurrentState();
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

        string lat = "";
        string lon = "";
        if (file.GpsDecimal != "—")
        {
            var p = file.GpsDecimal.Split(',');
            lat = p.Length > 0 ? p[0].Trim() : "";
            lon = p.Length > 1 ? p[1].Trim() : "";
        }
        
        GpsData.Add(new() { Key = "Latitud", Value = lat, Group = "GPS", IsEditable = true, GpsFormat = SelectedGpsFormat });
        GpsData.Add(new() { Key = "Longitud", Value = lon, Group = "GPS", IsEditable = true, GpsFormat = SelectedGpsFormat });

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

        foreach (var entry in GeneralInfo.Concat(GpsData).Concat(ExifData).Concat(IptcData).Concat(AuthorshipData))
        {
            entry.OriginalValue = entry.Value;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ScanDuplicatesAsync()
    {
        var filesToScan = Files.Where(f => !f.IsDirectory && 
            (GetCategoryForExtension(f.Extension) == "Image" || GetCategoryForExtension(f.Extension) == "Raw")).ToList();

        if (filesToScan.Count == 0)
        {
            StatusText = "No hay imágenes para escanear.";
            return;
        }

        IsScanningDuplicates = true;
        DuplicateScanProgress = 0;
        DuplicateGroups.Clear();
        StatusText = "Escaneando imágenes (Hash perceptual)...";

        await Task.Run(async () =>
        {
            var fileHashes = new Dictionary<FileItem, ulong>();
            int count = 0;

            foreach (var file in filesToScan)
            {
                ulong hash = DuplicateFinderService.ComputeDHash(file.FullPath);
                if (hash != 0) 
                {
                    fileHashes[file] = hash;
                }
                
                count++;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DuplicateScanProgress = (int)((count / (double)filesToScan.Count) * 50); 
                    StatusText = $"Calculando hashes ({count}/{filesToScan.Count})...";
                });
            }

            var groups = new List<List<FileItem>>();
            var processed = new HashSet<FileItem>();

            int groupingCount = 0;
            var hashesList = fileHashes.ToList();

            foreach (var kvp in hashesList)
            {
                if (processed.Contains(kvp.Key)) continue;

                var currentGroup = new List<FileItem> { kvp.Key };
                processed.Add(kvp.Key);

                foreach (var otherKvp in hashesList)
                {
                    if (processed.Contains(otherKvp.Key)) continue;

                    int distance = DuplicateFinderService.HammingDistance(kvp.Value, otherKvp.Value);
                    if (distance <= 5)
                    {
                        currentGroup.Add(otherKvp.Key);
                        processed.Add(otherKvp.Key);
                    }
                }

                if (currentGroup.Count > 1)
                {
                    groups.Add(currentGroup);
                }

                groupingCount++;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DuplicateScanProgress = 50 + (int)((groupingCount / (double)hashesList.Count) * 50); 
                });
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                int groupId = 1;
                foreach (var g in groups)
                {
                    DuplicateGroups.Add(new DuplicateGroup($"Grupo {groupId++}", g));
                }

                IsScanningDuplicates = false;
                DuplicateScanProgress = 100;
                StatusText = $"Escaneo completado. Se encontraron {DuplicateGroups.Count} grupos de duplicados.";
                
                // Si Auto Delete está activado, lanzar la eliminación automáticamente
                if (DuplicateAutoDelete && DuplicateGroups.Count > 0)
                {
                    _ = DeleteDuplicatesAsync();
                }
            });
        });
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task DeleteDuplicatesAsync()
    {
        var toDelete = new List<DuplicateItem>();
        foreach (var group in DuplicateGroups)
        {
            foreach (var item in group.Items)
            {
                if (item.IsSelectedForDeletion)
                {
                    toDelete.Add(item);
                }
            }
        }

        if (toDelete.Count == 0)
        {
            StatusText = "No hay duplicados marcados para eliminar.";
            return;
        }

        IsProcessing = true;
        StatusText = $"Eliminando {toDelete.Count} duplicados...";

        await Task.Run(() =>
        {
            foreach (var item in toDelete)
            {
                try
                {
                    if (File.Exists(item.FullPath))
                    {
                        File.Delete(item.FullPath);
                    }
                }
                catch { }
            }
        });

        IsProcessing = false;
        StatusText = $"{toDelete.Count} duplicados eliminados.";
        
        DuplicateGroups.Clear();
        _ = ReloadFilesFromPathAsync(CurrentPath);
    }
}
