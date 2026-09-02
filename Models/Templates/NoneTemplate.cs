using ImageMagick;
using System.Threading.Tasks;

namespace MegaDatos.Models.Templates;

public class NoneTemplate : IMetadataTemplate
{
    public string Id => "None";
    public string Name => "(Ninguna)";
    public string Description => "No se aplica ni evalúa ninguna plantilla. Los archivos conservan sus metadatos intactos sin advertencias, discrepancias ni comparaciones.";
    public string IconGlyph => "⚪";
    public string Category => "Sin Plantilla";
    public string PreservesSummary => "100% de los metadatos originales sin alterar.";
    public string RemovesSummary => "Ninguno.";
    public string InjectsSummary => "Ninguno.";

    public TemplateComplianceResult ValidateCompliance(FileItem file)
    {
        return new TemplateComplianceResult
        {
            State = ComplianceState.None,
            SummaryMessage = "Sin plantilla seleccionada"
        };
    }

    public void Apply(MagickImage image, FileItem file)
    {
        // No-op: No altera el archivo
    }

    public bool UsesExifTool => false;
    public Task ApplyWithExifToolAsync(string targetPath, FileItem file) => Task.CompletedTask;
}
