using System.Data;
using System.Security.Cryptography;
using System.Text;
using LSCode.Shell.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace LSCode.Infrastructure;

internal static class RequestTracking
{
    private const string RegistryPath = @"Software\FCoder";
    private const string RegistryValue = "Qlcv";
    private const string RegistryPassword = "QLcv";

    public static bool LegacyConnectionAvailable => TryLegacyTarget(out _);

    public static SqlTarget Resolve(SqlTarget? selected, bool useLegacy)
    {
        if (useLegacy && TryLegacyTarget(out var legacy)) return legacy;
        return selected ?? throw new InvalidOperationException(
            "Chưa có kết nối QLYC. Hãy mở kết nối SQL hoặc cấu hình Qlcv của FCode.");
    }

    public static async Task<DataTable> ReadAsync(
        SqlTarget target,
        string user,
        DateTime from,
        DateTime to,
        CancellationToken token)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.nbphyc', N'U') IS NULL
            BEGIN
                RAISERROR(N'Kết nối đã chọn không có bảng QLYC dbo.nbphyc.', 16, 1);
                RETURN;
            END;

            SELECT TOP (1000)
                RTRIM(stt_rec) AS [Số yêu cầu],
                RTRIM(ma_da) AS [Dự án],
                CONVERT(varchar(16), ngay_ht, 103) + ' ' + LEFT(CONVERT(varchar(8), ngay_ht, 108), 5) AS [Ngày cập nhật],
                RTRIM(ma_lt1) AS [Người xử lý],
                RTRIM(trang_thai) AS [Trạng thái],
                noi_dung AS [Nội dung]
            FROM dbo.nbphyc
            WHERE ngay_ht >= @from
              AND ngay_ht < DATEADD(day, 1, @to)
              AND (@user = N'' OR RTRIM(ma_lt1) = @user)
            ORDER BY ngay_ht DESC, stt_rec DESC;
            """;
        return await ProjectDatabase.ReadAsync(target, sql, token,
            new SqlParameter("@user", user.Trim()),
            new SqlParameter("@from", from.Date),
            new SqlParameter("@to", to.Date));
    }

    public static Task<DataTable> ReadCategoriesAsync(SqlTarget target, string requestId, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Cần số yêu cầu để xem đầu mục.");

        const string sql = """
            IF OBJECT_ID(N'dbo.nbdsdaumuclt', N'U') IS NULL OR OBJECT_ID(N'dbo.nbctdaumuc', N'U') IS NULL
            BEGIN
                RAISERROR(N'Kết nối QLYC chưa có dữ liệu danh mục hoặc chi tiết đầu mục.', 16, 1);
                RETURN;
            END;

            SELECT RTRIM(c.ma_daumuc) AS [Mã đầu mục], RTRIM(c.ten_daumuc) AS [Đầu mục],
                CASE WHEN LTRIM(RTRIM(CONVERT(varchar(20), d.chon))) = '1' THEN 1 ELSE 0 END AS [Chọn],
                COALESCE(d.so_luong, 0) AS [Số lượng],
                RTRIM(d.ma_lt) AS [Lập trình], RTRIM(d.ma_tester) AS [Tester]
            FROM dbo.nbdsdaumuclt c
            OUTER APPLY (
                SELECT TOP (1) chon, so_luong, ma_lt, ma_tester
                FROM dbo.nbctdaumuc
                WHERE stt_rec = @requestId AND ma_daumuc = c.ma_daumuc
                ORDER BY stt DESC, stt_rec0 DESC
            ) d
            ORDER BY c.ma_daumuc;
            """;
        return ProjectDatabase.ReadAsync(target, sql, token, new SqlParameter("@requestId", requestId.Trim()));
    }

    public static async Task<DataTable> StatisticsAsync(
        SqlTarget target,
        int year,
        CancellationToken token)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.nbphyc', N'U') IS NULL
            BEGIN
                RAISERROR(N'Kết nối đã chọn không có bảng QLYC dbo.nbphyc.', 16, 1);
                RETURN;
            END;

            ;WITH request_data AS (
                SELECT RTRIM(ma_lt1) AS employee, MONTH(ngay_ht) AS month_no, COUNT_BIG(1) AS request_count
                FROM dbo.nbphyc
                WHERE YEAR(ngay_ht) = @year
                  AND trang_thai IN ('HT', 'DT', 'UP', 'OK')
                  AND NULLIF(RTRIM(ma_lt1), '') IS NOT NULL
                GROUP BY RTRIM(ma_lt1), MONTH(ngay_ht)
            ), quantity_data AS (
                SELECT RTRIM(z.ma_lt) AS employee, MONTH(a.ngay_ht) AS month_no, SUM(CONVERT(decimal(28, 4), z.so_luong)) AS quantity
                FROM dbo.nbphyc a
                JOIN dbo.nbctdaumuc z ON a.stt_rec = z.stt_rec
                WHERE YEAR(a.ngay_ht) = @year
                  AND a.trang_thai IN ('HT', 'DT', 'UP', 'OK')
                  AND NULLIF(RTRIM(z.ma_lt), '') IS NOT NULL
                GROUP BY RTRIM(z.ma_lt), MONTH(a.ngay_ht)
            ), monthly AS (
                SELECT employee, month_no, request_count, CONVERT(decimal(28, 4), 0) AS quantity FROM request_data
                UNION ALL
                SELECT employee, month_no, 0, quantity FROM quantity_data
            ), combined AS (
                SELECT employee, month_no, SUM(request_count) AS request_count, SUM(quantity) AS quantity
                FROM monthly GROUP BY employee, month_no
            ), employees AS (
                SELECT employee FROM request_data
                UNION
                SELECT employee FROM quantity_data
            )
            SELECT
                N'' AS [Phòng ban], e.employee AS [Nhân viên],
                SUM(CASE WHEN c.month_no = 1 THEN c.request_count ELSE 0 END) AS [T1],
                SUM(CASE WHEN c.month_no = 2 THEN c.request_count ELSE 0 END) AS [T2],
                SUM(CASE WHEN c.month_no = 3 THEN c.request_count ELSE 0 END) AS [T3],
                SUM(CASE WHEN c.month_no = 4 THEN c.request_count ELSE 0 END) AS [T4],
                SUM(CASE WHEN c.month_no = 5 THEN c.request_count ELSE 0 END) AS [T5],
                SUM(CASE WHEN c.month_no = 6 THEN c.request_count ELSE 0 END) AS [T6],
                SUM(CASE WHEN c.month_no = 7 THEN c.request_count ELSE 0 END) AS [T7],
                SUM(CASE WHEN c.month_no = 8 THEN c.request_count ELSE 0 END) AS [T8],
                SUM(CASE WHEN c.month_no = 9 THEN c.request_count ELSE 0 END) AS [T9],
                SUM(CASE WHEN c.month_no = 10 THEN c.request_count ELSE 0 END) AS [T10],
                SUM(CASE WHEN c.month_no = 11 THEN c.request_count ELSE 0 END) AS [T11],
                SUM(CASE WHEN c.month_no = 12 THEN c.request_count ELSE 0 END) AS [T12],
                COALESCE(SUM(c.request_count), 0) AS [Tổng YC],
                CAST(COALESCE(AVG(CONVERT(decimal(18, 2), c.request_count)), 0) AS decimal(18, 2)) AS [TB YC],
                SUM(CASE WHEN c.month_no = 1 THEN c.quantity ELSE 0 END) AS [SL T1],
                SUM(CASE WHEN c.month_no = 2 THEN c.quantity ELSE 0 END) AS [SL T2],
                SUM(CASE WHEN c.month_no = 3 THEN c.quantity ELSE 0 END) AS [SL T3],
                SUM(CASE WHEN c.month_no = 4 THEN c.quantity ELSE 0 END) AS [SL T4],
                SUM(CASE WHEN c.month_no = 5 THEN c.quantity ELSE 0 END) AS [SL T5],
                SUM(CASE WHEN c.month_no = 6 THEN c.quantity ELSE 0 END) AS [SL T6],
                SUM(CASE WHEN c.month_no = 7 THEN c.quantity ELSE 0 END) AS [SL T7],
                SUM(CASE WHEN c.month_no = 8 THEN c.quantity ELSE 0 END) AS [SL T8],
                SUM(CASE WHEN c.month_no = 9 THEN c.quantity ELSE 0 END) AS [SL T9],
                SUM(CASE WHEN c.month_no = 10 THEN c.quantity ELSE 0 END) AS [SL T10],
                SUM(CASE WHEN c.month_no = 11 THEN c.quantity ELSE 0 END) AS [SL T11],
                SUM(CASE WHEN c.month_no = 12 THEN c.quantity ELSE 0 END) AS [SL T12],
                COALESCE(SUM(c.quantity), 0) AS [Tổng SL],
                CAST(COALESCE(AVG(c.quantity), 0) AS decimal(18, 2)) AS [TB SL]
            FROM employees e
            LEFT JOIN combined c ON c.employee = e.employee
            GROUP BY e.employee
            ORDER BY e.employee;
            """;
        return await ProjectDatabase.ReadAsync(target, sql, token, new SqlParameter("@year", year));
    }

    private static bool TryLegacyTarget(out SqlTarget target)
    {
        target = null!;
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                using var key = root.OpenSubKey(RegistryPath);
                var encrypted = Convert.ToString(key?.GetValue(RegistryValue))?.Trim();
                if (string.IsNullOrEmpty(encrypted)) continue;
                var raw = Decrypt(encrypted, RegistryPassword);
                var builder = new SqlConnectionStringBuilder(raw);
                if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog)) continue;
                if (!new System.Data.Common.DbConnectionStringBuilder { ConnectionString = raw }.ContainsKey("Encrypt"))
                    builder.Encrypt = SqlConnectionEncryptOption.Optional;
                builder.ApplicationName = "LS Code";
                target = new SqlTarget(builder.DataSource, builder.InitialCatalog, builder.ConnectionString);
                return true;
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException or ArgumentException
                or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Continue with the LSCode connection when the optional legacy setting is unreadable or invalid.
            }
        }
        return false;
    }

#pragma warning disable SYSLIB0041
    private static string Decrypt(string cipherText, string password)
    {
        using var derive = new PasswordDeriveBytes(password, new byte[33]);
        using var algorithm = Aes.Create();
        algorithm.Key = derive.GetBytes(32);
        algorithm.IV = derive.GetBytes(16);
        using var output = new MemoryStream();
        using (var crypto = new CryptoStream(output, algorithm.CreateDecryptor(), CryptoStreamMode.Write))
        {
            var bytes = Convert.FromBase64String(cipherText);
            crypto.Write(bytes, 0, bytes.Length);
        }
        return Encoding.Unicode.GetString(output.ToArray());
    }
#pragma warning restore SYSLIB0041
}
