using System.Globalization;
using System.Text.Json;

namespace DynamicTemplateStudio.Core.Services;

public static class ValueConverter
{
    public static object? Convert(JsonElement value, string type, int? maxLength = null)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        maxLength ??= SqlTypes.Length(type);
        switch (SqlTypes.Base(type))
        {
            case "string":
            case "char":
            case "varchar":
            case "nvarchar":
                if (value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) throw new FormatException("Expected a scalar string.");
                if (maxLength.HasValue && text.Length > maxLength) throw new FormatException("String exceeds maximum length.");
                return text;
            case "int": return int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            case "bigint": return long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            case "bool": return bool.Parse(text);
            case "bit": return text switch { "0" => false, "1" => true, _ => bool.Parse(text) };
            case "guid": return Guid.Parse(text).ToString();
            case "date": return DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd");
            case "datetime": return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).ToString("O");
            case "smalldatetime":
                if (System.Text.RegularExpressions.Regex.IsMatch(text, @"(?:Z|[+-]\d{2}:\d{2})$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new FormatException("smalldatetime has no time zone; supply a local date/time without offset.");
                var time = DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
                if (time < new DateTime(1900, 1, 1) || time >= new DateTime(2079, 6, 7)) throw new OverflowException("smalldatetime must be between 1900-01-01 and 2079-06-06.");
                var minute = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0);
                if ((time - minute).TotalMilliseconds >= 29999) minute = minute.AddMinutes(1);
                if (minute >= new DateTime(2079, 6, 7)) throw new OverflowException("Rounded smalldatetime exceeds 2079-06-06.");
                return minute.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            default:
                if (!SqlTypes.IsNumeric(type)) throw new FormatException("Unsupported type.");
                var (precision, scale) = Validation.TemplateValidator.DecimalParts(type);
                var number = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                if (decimal.Round(number, Math.Min(scale, 28)) != number) throw new FormatException("Value exceeds decimal scale; rounding is not automatic.");
                var digits = decimal.Truncate(decimal.Abs(number)).ToString(CultureInfo.InvariantCulture).TrimStart('0').Length;
                if (digits > precision - scale) throw new OverflowException("Value exceeds decimal precision.");
                return number;
        }
    }
}
