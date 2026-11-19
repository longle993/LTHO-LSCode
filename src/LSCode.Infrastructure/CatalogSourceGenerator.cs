using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LSCode.Core;

namespace LSCode.Infrastructure;

internal static class CatalogSourceGenerator
{
    internal sealed record Input(string Root, string Template, string Controller, string TitleVi, string TitleEn,
        Dictionary<string, string>? Tables, Dictionary<string, string>? Fields);
    internal sealed record SourceFile(string Path, string Text, Encoding Encoding, bool Bom);
    internal sealed record Draft(string Controller, SourceFile[] Files, string Fingerprint, bool HasMain);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private const string SqlIdentifier = @"[A-Za-z_][A-Za-z0-9_$]{0,127}";

    public static async Task<MenuEditor.ResourceResult> DiscoverAsync(string root, string template, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(root) || !Directory.Exists(root))
            throw new ArgumentException("Thư mục source dự án không tồn tại.");
        if (string.IsNullOrWhiteSpace(template))
            throw new ArgumentException("Nhập đường dẫn Main, page ID hoặc SysId của mẫu.");
        var source = await MenuEditor.ResourcesAsync(root, template.Trim(), template.Trim(), token).ConfigureAwait(false);
        if (source.Controllers.Length != 1 || !source.Resources.Any(resource => resource.Kind == "Grid" && resource.Paths.Length > 0))
            throw new InvalidOperationException("Mẫu phải có một controller chính và ít nhất một file Grid.");
        if (source.Resources.Where(resource => resource.Kind == "Main").SelectMany(resource => resource.Paths)
            .Any(path => !Path.GetExtension(path).Equals(".aspx", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("File Main của mẫu phải là ASPX.");
        return source;
    }

    public static async Task<Draft> PreviewAsync(Input input, CancellationToken token)
    {
        var controller = input.Controller?.Trim() ?? "";
        if (!Regex.IsMatch(controller, @"\A[A-Za-z_][A-Za-z0-9_]{0,63}\z", RegexOptions.None, Timeout) ||
            Regex.IsMatch(controller, @"\A(?:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z", RegexOptions.IgnoreCase, Timeout))
            throw new ArgumentException("Controller mới phải có 1–64 ký tự chữ Latin, số hoặc dấu gạch dưới và bắt đầu bằng chữ hoặc dấu gạch dưới.");
        var source = await DiscoverAsync(input.Root, input.Template, token).ConfigureAwait(false);
        var original = source.Controllers.Single();
        var main = source.Resources.Single(resource => resource.Kind == "Main").Paths.SingleOrDefault();
        var paths = source.Resources.SelectMany(resource => resource.Paths).ToArray();
        if (paths.Length > 60) throw new InvalidOperationException("Mẫu có quá 60 file controller.");
        var comparer = StringComparer.OrdinalIgnoreCase;
        if (comparer.Equals(original, controller)) throw new ArgumentException("Controller mới phải khác controller mẫu.");
        var names = new Dictionary<string, string>(comparer) { [original] = controller };
        foreach (var stem in paths.Where(path => path != main).Select(Path.GetFileNameWithoutExtension).Distinct(comparer))
            if (!names.ContainsKey(stem!)) names.Add(stem!, controller + "_" + stem);

        var replacements = new Dictionary<string, string>(names, comparer);
        if (main is not null) AddMapping(replacements, Path.GetFileNameWithoutExtension(main), controller);
        foreach (var (key, replacement) in input.Tables ?? [])
        {
            if (!source.Tables.Contains(key, comparer))
                throw new ArgumentException("Bảng không thuộc mẫu hiện tại: " + key);
            var normalized = key.Replace("[", "").Replace("]", "");
            if (!Regex.IsMatch(normalized, $@"\A{SqlIdentifier}(?:\.{SqlIdentifier})?\z", RegexOptions.None, Timeout) ||
                !Regex.IsMatch(replacement, $@"\A{SqlIdentifier}(?:\.{SqlIdentifier})?\z", RegexOptions.None, Timeout))
                throw new ArgumentException("Tên bảng ánh xạ phải có dạng bảng hoặc schema.bảng: " + key);
            var from = normalized.Split('.');
            var to = replacement.Split('.');
            if (from.Length != to.Length)
                throw new ArgumentException("Giữ cùng dạng bảng hoặc schema.bảng cho ánh xạ: " + key);
            for (var mask = 0; mask < (1 << from.Length); mask++)
            {
                string Format(string[] parts) => string.Join('.', parts.Select((part, index) =>
                    (mask & (1 << index)) == 0 ? part : "[" + part + "]"));
                AddMapping(replacements, Format(from), Format(to));
            }
        }
        foreach (var (key, replacement) in input.Fields ?? [])
        {
            if (!Regex.IsMatch(key, $@"\A{SqlIdentifier}\z", RegexOptions.None, Timeout) ||
                !Regex.IsMatch(replacement, $@"\A{SqlIdentifier}\z", RegexOptions.None, Timeout))
                throw new ArgumentException("Tên cột ánh xạ không hợp lệ: " + key);
            AddMapping(replacements, key, replacement);
        }
        foreach (var table in source.Tables)
        {
            var normalized = table.Replace("[", "").Replace("]", "");
            var shortName = normalized.Split('.').Last();
            if ((names.ContainsKey(shortName) || replacements.ContainsKey(shortName)) &&
                input.Tables?.Keys.Contains(table, comparer) != true)
                throw new ArgumentException("Tên bảng trùng tên cần đổi trong mẫu: " + table +
                    ". Hãy khai báo ánh xạ bảng rõ ràng hoặc chọn mẫu khác.");
        }
        var pattern = "(?<![\\w$])(?:" + string.Join('|', replacements.Keys.OrderByDescending(key => key.Length).Select(Regex.Escape)) + ")(?![\\w$])";
        var remap = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
        var files = new List<SourceFile>();
        var outputPaths = new HashSet<string>(comparer);
        long size = 0;
        foreach (var path in paths)
        {
            token.ThrowIfCancellationRequested();
            var absolute = Path.Combine(input.Root, path);
            size += new FileInfo(absolute).Length;
            if (size > 8 * 1024 * 1024) throw new IOException("Tổng kích thước file mẫu vượt 8 MB.");
            var snapshot = await DocumentStore.OpenAsync(absolute, token).ConfigureAwait(false);
            var stem = Path.GetFileNameWithoutExtension(path);
            var name = path == main ? controller : names[stem];
            var output = Path.Combine(Path.GetDirectoryName(path) ?? "", name + Path.GetExtension(path));
            if (!outputPaths.Add(output)) throw new InvalidOperationException("Trùng đường dẫn xuất: " + output);
            if (File.Exists(Path.Combine(input.Root, output)))
                throw new IOException("Source dự án đã có file: " + output);
            var text = remap.Replace(snapshot.Text, match => replacements[match.Value]);
            if (path != main && comparer.Equals(stem, original))
                text = UpdateTitle(text, input.TitleVi ?? "", input.TitleEn ?? "");
            files.Add(new(output, text, snapshot.Encoding, snapshot.Bom));
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            files.Select(file => new { file.Path, file.Text, file.Encoding.CodePage, file.Bom }))));
        return new(controller, files.ToArray(), fingerprint, main is not null);
    }

    public static async Task<object> ExportAsync(Input input, string fingerprint, string parent, CancellationToken token)
    {
        if (!Path.IsPathFullyQualified(parent) || !Directory.Exists(parent))
            throw new ArgumentException("Chọn thư mục xuất đang tồn tại.");
        var draft = await PreviewAsync(input, token).ConfigureAwait(false);
        if (!string.Equals(draft.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw new InvalidOperationException("Mẫu hoặc cấu hình đã thay đổi. Hãy xem trước lại.");
        var folder = Path.Combine(parent, draft.Controller);
        if (Directory.Exists(folder) || File.Exists(folder))
            throw new IOException("Thư mục xuất đã tồn tại: " + folder);
        var staging = Path.Combine(parent, ".lscode-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            foreach (var file in draft.Files)
            {
                var path = Path.Combine(staging, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var bytes = file.Encoding.GetBytes(file.Text);
                if (file.Bom) bytes = [.. file.Encoding.GetPreamble(), .. bytes];
                await File.WriteAllBytesAsync(path, bytes, token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, folder);
            return new { folder, files = draft.Files.Select(file => Path.Combine(folder, file.Path)).ToArray() };
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void AddMapping(Dictionary<string, string> values, string from, string to)
    {
        if (values.TryGetValue(from, out var mapped) && !StringComparer.OrdinalIgnoreCase.Equals(mapped, to))
            throw new ArgumentException("Hai ánh xạ khác nhau cho: " + from);
        values[from] = to;
    }

    private static string UpdateTitle(string text, string vietnamese, string english)
    {
        if (vietnamese.Length == 0 && english.Length == 0) return text;
        var title = new Regex("""<title\b[^>]*>""", RegexOptions.IgnoreCase, Timeout);
        return title.Replace(text, tag =>
        {
            var attributes = new Regex("""(?<prefix>\s(?<lang>v|e)\s*=\s*)(?<quote>['"])(?:.*?)\k<quote>""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline, Timeout);
            return attributes.Replace(tag.Value, attr =>
            {
                var value = attr.Groups["lang"].Value.Equals("v", StringComparison.OrdinalIgnoreCase) ? vietnamese : english;
                return value.Length == 0 ? attr.Value : attr.Groups["prefix"].Value + '"' + SecurityElement.Escape(value) + '"';
            });
        }, 1);
    }
}
