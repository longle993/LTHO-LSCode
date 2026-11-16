using DynamicTemplateStudio.Core.Models;

namespace DynamicTemplateStudio.Core.Interfaces;

public interface ITemplateSerializer
{
    string Serialize(TemplateDefinition template);
    TemplateDefinition Deserialize(string json);
}
public interface IMappingPreviewService
{
    MappingResult Run(TemplateDefinition template, string sample, CancellationToken cancellation = default, int rowLimit = 100000);
}
public interface ISampleInferenceService
{
    TemplateDefinition Infer(string sample);
}
