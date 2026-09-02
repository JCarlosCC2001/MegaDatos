using System.Collections.Generic;

namespace MegaDatos.Models.Templates;

public enum ComplianceState
{
    None,
    Compliant,       // 🟢 Cumple completamente con la plantilla
    NonCompliant,    // 🔴 Discrepancia / Contiene metadatos no permitidos
    Incomplete       // 🟡 Incompleto / Faltan datos requeridos por la plantilla
}

public class TemplateComplianceResult
{
    public ComplianceState State { get; set; } = ComplianceState.None;
    public string SummaryMessage { get; set; } = string.Empty;
    public List<string> Details { get; set; } = new();

    public string BadgeText => State switch
    {
        ComplianceState.Compliant => "✓ Cumple",
        ComplianceState.NonCompliant => "✕ Discrepancia",
        ComplianceState.Incomplete => "⚠ Incompleto",
        _ => "—"
    };

    public string BadgeColor => State switch
    {
        ComplianceState.Compliant => "#10b981",    // Verde esmeralda
        ComplianceState.NonCompliant => "#ef4444", // Rojo
        ComplianceState.Incomplete => "#f59e0b",   // Amarillo ámbar
        _ => "#6b7280"                            // Gris
    };

    public string BadgeBackground => State switch
    {
        ComplianceState.Compliant => "#1a10b981",
        ComplianceState.NonCompliant => "#1aef4444",
        ComplianceState.Incomplete => "#1af59e0b",
        _ => "Transparent"
    };
}
