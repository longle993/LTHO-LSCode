using System.Text.Json;
using DynamicTemplateStudio.Core.Models;
using DynamicTemplateStudio.Core.Services;
using DynamicTemplateStudio.Core.Validation;
using DynamicTemplateStudio.Infrastructure.JsonPath;
using DynamicTemplateStudio.Infrastructure.ProjectPersistence;
using DynamicTemplateStudio.Infrastructure.Serialization;

namespace LSCode.Infrastructure;

internal sealed class TemplateStudioTool
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly TemplateSerializer serializer = new();
    private readonly SemaphoreSlim gate = new(1);
    private ProjectRepository? repository;
    private string? folder;

    private static object[] SaveIssues(ProjectDefinition project, IReadOnlyList<(int TemplateIndex, Diagnostic Issue)> issues)
        => issues.Select(entry => (object)new
        {
            templateCode = project.Templates[entry.TemplateIndex].TemplateCode,
            entry.Issue.Table,
            entry.Issue.Field,
            entry.Issue.TableIndex,
            entry.Issue.FieldIndex,
            entry.Issue.Location,
            entry.Issue.Message
        }).ToArray();

    public async Task<object> ExecuteAsync(string method, JsonElement payload, CancellationToken token)
    {
        var usesRepository = method is "templateStudio.open" or "templateStudio.create" or "templateStudio.recover"
            or "templateStudio.save" or "templateStudio.saveTemplate" or "templateStudio.export";
        if (usesRepository) await gate.WaitAsync(token);
        try
        {
            if (method is "templateStudio.open" or "templateStudio.create" or "templateStudio.recover")
            {
                var path = payload.GetProperty("folder").GetString() ?? "";
                if (!Path.IsPathFullyQualified(path))
                    throw new ArgumentException("Chọn thư mục project Studio.");
                var selected = new DirectoryInfo(path);
                if (method != "templateStudio.create" && selected.Name.Equals("templates", StringComparison.OrdinalIgnoreCase)
                    && !File.Exists(Path.Combine(path, "project.json")) && selected.Parent is { } parent
                    && File.Exists(Path.Combine(parent.FullName, "project.json")))
                    path = parent.FullName;
                var next = new ProjectRepository(serializer);
                if (method == "templateStudio.recover") await next.RecoverAsync(path);
                ProjectDefinition project;
                if (method == "templateStudio.create")
                {
                    project = new ProjectDefinition { ProjectName = new DirectoryInfo(path).Name };
                    await next.ExportAsync(project, path);
                    project = await next.OpenAsync(path, token);
                }
                else project = await next.OpenAsync(path, token);
                var draftPath = ProjectRepository.SafePath(path, ".studio/draft.json");
                var draft = File.Exists(draftPath)
                    ? DocumentState.Restore(await File.ReadAllTextAsync(draftPath, token)) : null;
                if (draft is not null)
                {
                    var draftCodes = draft.Templates.Select(item => item.TemplateCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var draftIds = draft.Templates.Select(item => item.Id).ToHashSet();
                    foreach (var item in project.Templates.Where(item => next.DiscoveredTemplateCodes.Contains(item.TemplateCode)
                        && !draftCodes.Contains(item.TemplateCode) && !draftIds.Contains(item.Id)))
                        draft.Templates.Add(item);
                }
                repository = next;
                folder = path;
                return new { project, draft, skippedTemplates = next.SkippedTemplates.ToArray() };
            }

            if ((method is "templateStudio.save" or "templateStudio.export") && repository is not null && folder is not null)
            {
                var project = payload.GetProperty("project").Deserialize<ProjectDefinition>(Json)
                    ?? throw new ArgumentException("Thiếu project Studio.");
                if (method == "templateStudio.save")
                {
                    var issues = await repository.SaveWithDiagnosticsAsync(project, folder, token);
                    return new { valid = issues.Count == 0, issues = SaveIssues(project, issues) };
                }
                await repository.ExportAsync(project, payload.GetProperty("folder").GetString() ?? "");
                return true;
            }

            if (method == "templateStudio.saveTemplate" && repository is not null && folder is not null)
            {
                var current = payload.GetProperty("project").Deserialize<ProjectDefinition>(Json)
                    ?? throw new ArgumentException("Thiếu project Studio.");
                var selected = current.Templates[payload.GetProperty("template").GetInt32()];
                var persisted = await repository.OpenAsync(folder, token);
                persisted.ProjectName = current.ProjectName;
                persisted.Description = current.Description;
                var existing = persisted.Templates.FirstOrDefault(item => item.Id == selected.Id)
                    ?? persisted.Templates.FirstOrDefault(item => item.TemplateCode.Equals(selected.TemplateCode, StringComparison.OrdinalIgnoreCase));
                if (existing is null) persisted.Templates.Add(selected);
                else persisted.Templates[persisted.Templates.IndexOf(existing)] = selected;
                var issues = await repository.SaveWithDiagnosticsAsync(persisted, folder, token);
                return new { valid = issues.Count == 0, project = persisted, issues = SaveIssues(persisted, issues) };
            }

            if (method == "templateStudio.exportTemplate")
            {
                var template = payload.GetProperty("template").Deserialize<TemplateDefinition>(Json)
                    ?? throw new ArgumentException("Thiếu template.");
                var errors = TemplateValidator.Validate(template, true);
                if (errors.Count > 0) throw new InvalidOperationException(errors[0].ToString());
                return serializer.Serialize(template);
            }

            if (method == "templateStudio.infer")
            {
                var sample = payload.GetProperty("sample").GetString() ?? "";
                return await Task.Run(() => new SampleInferenceService().Infer(sample), token);
            }

            if (method is "templateStudio.json" or "templateStudio.mapping" or "templateStudio.renameField"
                or "templateStudio.renameTable" or "templateStudio.parent" or "templateStudio.synchronize"
                or "templateStudio.copyFields" or "templateStudio.pasteFields")
            {
                var template = payload.GetProperty("template").Deserialize<TemplateDefinition>(Json)
                    ?? throw new ArgumentException("Thiếu template.");
                if (method is not ("templateStudio.json" or "templateStudio.mapping"))
                {
                    var table = template.Tables[payload.GetProperty("table").GetInt32()];
                    switch (method)
                    {
                        case "templateStudio.renameField":
                            GraphEditor.RenameField(template, table, table.Columns[payload.GetProperty("field").GetInt32()],
                                payload.GetProperty("name").GetString() ?? "");
                            break;
                        case "templateStudio.renameTable":
                            GraphEditor.RenameTable(template, table, payload.GetProperty("name").GetString() ?? "");
                            RelationshipEditor.Synchronize(template, false);
                            break;
                        case "templateStudio.parent":
                            var parent = payload.GetProperty("parent").GetInt32();
                            if (parent >= 0 && (template.Tables[parent] == table ||
                                RelationshipEditor.Ancestors(template, template.Tables[parent]).Contains(table)))
                                throw new InvalidOperationException("Quan hệ cha–con tạo vòng lặp.");
                            table.Parent = parent < 0 ? "" : template.Tables[parent].Name;
                            RelationshipEditor.Synchronize(template);
                            break;
                        case "templateStudio.synchronize":
                            RelationshipEditor.Synchronize(template, !payload.TryGetProperty("generateMissing", out var generate) || generate.GetBoolean());
                            break;
                        case "templateStudio.copyFields":
                            return FieldTransfer.Capture(payload.GetProperty("fields").EnumerateArray()
                                .Select(index => table.Columns[index.GetInt32()]));
                        case "templateStudio.pasteFields":
                            foreach (var field in FieldTransfer.Prepare(table, payload.GetProperty("text").GetString() ?? ""))
                                table.Columns.Add(field);
                            break;
                    }
                    return template;
                }
                var diagnostics = TemplateValidator.Validate(template, runtimeExport: method == "templateStudio.json");
                if (diagnostics.Any(issue => !issue.IsWarning)) return new { diagnostics };
                if (method == "templateStudio.json") return new { diagnostics, json = serializer.Serialize(template) };
                return await Task.Run(() => new MappingPreviewService().Run(template, template.SampleJson, token, 5000), token);
            }

            throw new InvalidOperationException("Mở project Studio trước khi lưu.");
        }
        finally { if (usesRepository) gate.Release(); }
    }
}
