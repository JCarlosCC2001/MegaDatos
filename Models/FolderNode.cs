using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MegaDatos.Models;

public partial class FolderNode : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<FolderNode> Children { get; set; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value) && value)
            {
                LoadChildren();
            }
        }
    }

    public FolderNode() { }

    public FolderNode(string name, string fullPath, bool isDummy = false)
    {
        Name = name;
        FullPath = fullPath;
        
        if (!isDummy && !string.IsNullOrEmpty(fullPath))
        {
            try
            {
                // Si tiene al menos una subcarpeta, agregamos un dummy para mostrar la flechita
                if (Directory.EnumerateDirectories(fullPath).Any())
                {
                    Children.Add(new FolderNode("⏳ Cargando...", "", true));
                }
            }
            catch { }
        }
    }

    private async void LoadChildren()
    {
        if (Children.Count == 1 && Children[0].FullPath == "")
        {
            try
            {
                var dirs = await Task.Run(() =>
                {
                    try
                    {
                        var list = new System.Collections.Generic.List<FolderNode>();
                        var directories = Directory.GetDirectories(FullPath);
                        foreach (var dir in directories)
                        {
                            list.Add(new FolderNode($"📁 {Path.GetFileName(dir)}", dir));
                        }
                        return list;
                    }
                    catch
                    {
                        return new System.Collections.Generic.List<FolderNode>();
                    }
                });

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Children.Clear();
                    foreach (var d in dirs)
                    {
                        Children.Add(d);
                    }
                });
            }
            catch 
            { 
                await Dispatcher.UIThread.InvokeAsync(() => Children.Clear());
            }
        }
    }
}
