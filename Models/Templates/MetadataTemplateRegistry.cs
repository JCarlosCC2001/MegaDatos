using System;
using System.Collections.Generic;
using System.Linq;

namespace MegaDatos.Models.Templates;

public class MetadataTemplateRegistry
{
    private static readonly Lazy<MetadataTemplateRegistry> _instance = new(() => new MetadataTemplateRegistry());
    public static MetadataTemplateRegistry Instance => _instance.Value;

    private readonly List<IMetadataTemplate> _templates = new();

    public IReadOnlyList<IMetadataTemplate> Templates => _templates;

    public MetadataTemplateRegistry()
    {
        // Registrar plantillas por defecto
        RegisterTemplate(new NoneTemplate());
        RegisterTemplate(new PhotoshopTemplate());
        RegisterTemplate(new WhatsAppTemplate());
        RegisterTemplate(new TimeStampTemplate());
        RegisterTemplate(new ScreenshotTemplate());
    }

    public void RegisterTemplate(IMetadataTemplate template)
    {
        if (!_templates.Any(t => string.Equals(t.Id, template.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _templates.Add(template);
        }
    }

    public IMetadataTemplate? GetById(string id)
    {
        return _templates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
