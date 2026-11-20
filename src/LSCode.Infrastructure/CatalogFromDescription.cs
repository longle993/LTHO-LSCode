using System.Security.Cryptography;
using System.Text.Json;
using LSCode.Domain;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal static class CatalogFromDescription
{
    internal sealed record Input(CatalogSourceGenerator.Input Source, string Table, CatalogField[] Fields,
        Dictionary<string, string?> Menu);

    internal static async Task<(CatalogSourceGenerator.Draft Source, string Sql, string Fingerprint)> PreviewAsync(
        Input input, SqlTarget app, SqlTarget sys, CancellationToken token)
    {
        if (!app.Server.Equals(sys.Server, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("App và Sys phải cùng máy chủ SQL.");
        if (input.Fields.Length == 0 || !input.Fields.Any(field => field.Key))
            throw new ArgumentException("Thêm ít nhất một trường khóa chính.");
        if (input.Source.Tables is not { Count: 1 })
            throw new ArgumentException("Chọn mẫu danh mục có đúng một bảng và ánh xạ bảng đó.");
        if (input.Source.Fields?.Values.Any(name =>
            !input.Fields.Any(field => field.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) == true)
            throw new ArgumentException("Cột đích trong ánh xạ phải thuộc lưới trường đã duyệt.");
        var source = await CatalogSourceGenerator.PreviewAsync(input.Source, token);
        if (!source.HasMain)
            throw new InvalidOperationException("Chọn mẫu có Main ASPX để sinh trang ERP.");
        var sql = CatalogDefinition.Build("dbo", input.Table, true, input.Fields, [], false, false);
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { source.Fingerprint, sql, input.Menu, App = app.Database, Sys = sys.Database, sys.Server })));
        return (source, sql, fingerprint);
    }

    internal static async Task<string[]> ApplyAsync(Input input, SqlTarget app, SqlTarget sys,
        string fingerprint, CancellationToken token)
    {
        var preview = await PreviewAsync(input, app, sys, token);
        if (preview.Fingerprint != fingerprint)
            throw new InvalidOperationException("Bản thiết kế đã thay đổi. Hãy xem trước lại.");

        var staged = new List<(string Temp, string Path)>();
        var published = new List<string>();
        var committing = false;
        try
        {
            foreach (var file in preview.Source.Files)
            {
                var path = Path.Combine(input.Source.Root, file.Path);
                var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                staged.Add((temp, path));
                var bytes = file.Encoding.GetBytes(file.Text);
                if (file.Bom) bytes = [.. file.Encoding.GetPreamble(), .. bytes];
                await File.WriteAllBytesAsync(temp, bytes, token);
            }
            await MenuEditor.SaveAsync(sys, "new", "", "", input.Menu, token, async (connection, transaction, ct) =>
            {
                if (input.Menu.TryGetValue("wmenu_id0", out var parent) && !string.IsNullOrWhiteSpace(parent))
                {
                    await using var parentCheck = new SqlCommand(
                        "SELECT COUNT(*) FROM dbo.wcommand WHERE wmenu_id=@parent", connection, transaction);
                    parentCheck.Parameters.AddWithValue("@parent", parent);
                    if (Convert.ToInt32(await parentCheck.ExecuteScalarAsync(ct)) == 0)
                        throw new InvalidOperationException("Menu cha không còn tồn tại. Chọn lại menu và xem trước.");
                }
                var database = CatalogDefinition.Quote(app.Database);
                await using var command = new SqlCommand($"USE {database}; \n{preview.Sql}",
                    connection, transaction) { CommandTimeout = 0 };
                await command.ExecuteNonQueryAsync(ct);
                foreach (var file in staged)
                {
                    ct.ThrowIfCancellationRequested();
                    File.Move(file.Temp, file.Path);
                    published.Add(file.Path);
                }
                committing = true;
            });
            return published.ToArray();
        }
        catch (Exception error)
        {
            if (committing)
                throw new InvalidOperationException("Chưa xác định trạng thái commit. Kiểm tra bảng và menu trước khi thử lại. Source đã được ghi.", error);
            foreach (var path in published) File.Delete(path);
            throw;
        }
        finally
        {
            foreach (var file in staged) File.Delete(file.Temp);
        }
    }
}
