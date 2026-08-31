using System.Collections.ObjectModel;

namespace MegaDatos.Models;

public class FolderNode
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<FolderNode> Children { get; set; } = new();
    public bool IsExpanded { get; set; }
}
