using ImageMagick;
using System.Threading.Tasks;

namespace MegaDatos.Models.Templates;

public interface IMetadataTemplate
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string IconGlyph { get; }
    string Category { get; }
    string PreservesSummary { get; }
    string RemovesSummary { get; }
    string InjectsSummary { get; }

    TemplateComplianceResult ValidateCompliance(FileItem file);
    void Apply(MagickImage image, FileItem file);
    
    bool UsesExifTool { get; }
    Task ApplyWithExifToolAsync(string targetPath, FileItem file);
}
