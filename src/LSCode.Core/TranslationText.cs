using System.Text;
using System.Text.Json;

namespace LSCode.Core;

public static class TranslationText
{
    public static string ReadGoogle(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0 || document.RootElement[0].ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Google Dịch không trả về nội dung.");
        var builder = new StringBuilder();
        foreach (var sentence in document.RootElement[0].EnumerateArray())
        {
            if (sentence.ValueKind != JsonValueKind.Array || sentence.GetArrayLength() == 0 || sentence[0].ValueKind != JsonValueKind.String) continue;
            builder.Append(sentence[0].GetString());
        }
        if (builder.Length == 0) throw new InvalidOperationException("Google Dịch không trả về nội dung.");
        return builder.ToString();
    }
}
