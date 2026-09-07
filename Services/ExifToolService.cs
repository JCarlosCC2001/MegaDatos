using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace MegaDatos.Services;

public class ExifToolResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public static class ExifToolService
{
    private static string GetExifToolPath()
    {
        string exiftoolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "exiftool(-k).exe");
        if (!File.Exists(exiftoolPath))
        {
            exiftoolPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "exiftool(-k).exe");
        }
        return exiftoolPath;
    }

    /// <summary>
    /// Executes ExifTool with the given arguments and supports retries.
    /// </summary>
    public static async Task<ExifToolResult> ExecuteAsync(List<string> args, int maxRetries = 1)
    {
        var psi = new ProcessStartInfo
        {
            FileName = GetExifToolPath(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        ExifToolResult finalResult = new ExifToolResult { Success = false };

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var process = Process.Start(psi);
                if (process != null)
                {
                    var errTask = process.StandardError.ReadToEndAsync();
                    var outTask = process.StandardOutput.ReadToEndAsync();
                    
                    await Task.WhenAll(errTask, outTask, process.WaitForExitAsync());
                    
                    finalResult.Error = errTask.Result;
                    finalResult.Output = outTask.Result;
                    
                    if (process.ExitCode == 0)
                    {
                        finalResult.Success = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                finalResult.Error = ex.Message;
            }

            if (!finalResult.Success && i < maxRetries - 1)
            {
                await Task.Delay(500);
            }
        }

        return finalResult;
    }
}
