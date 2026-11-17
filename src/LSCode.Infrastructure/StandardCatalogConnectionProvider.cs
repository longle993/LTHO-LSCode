using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient;

namespace LSCode.Infrastructure;

internal interface IStandardCatalogConnectionProvider
{
    string GetConnectionString();
}

internal sealed class EmbeddedStandardCatalogConnectionProvider : IStandardCatalogConnectionProvider
{
    private const string ResourceName = "LSCode.Infrastructure.StandardCatalogConnection";

    public string GetConnectionString()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("LSCode thiếu cấu hình kết nối danh mục chuẩn.");
        using var reader = new StreamReader(stream, Encoding.UTF8, false, leaveOpen: false);
        var encoded = reader.ReadToEnd().Trim();
        if (encoded.Length == 0 || encoded.Length > 8_192)
            throw new InvalidDataException("Cấu hình kết nối danh mục chuẩn không hợp lệ.");

        string decoded;
        try
        {
            var padded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
            decoded = Encoding.UTF32.GetString(Convert.FromBase64String(padded));
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("Cấu hình kết nối danh mục chuẩn không đúng định dạng.", ex);
        }

        var connection = new SqlConnectionStringBuilder(decoded);
        if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog))
            throw new InvalidDataException("Cấu hình kết nối danh mục chuẩn thiếu server hoặc database.");
        connection.ConnectTimeout = 15;
        connection.ApplicationName = "LS Code Standard Project Sync";
        if (!decoded.Contains("Encrypt=", StringComparison.OrdinalIgnoreCase))
            connection.Encrypt = SqlConnectionEncryptOption.Optional;
        return connection.ConnectionString;
    }
}
