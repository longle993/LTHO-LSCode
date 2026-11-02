using System.Net;

namespace LSCode.Core;

public sealed class TranslationRateLimitException(DateTimeOffset retryAt) : Exception("Google đang giới hạn yêu cầu. Dùng nút Mở Google Dịch hoặc thử lại sau.")
{
    public DateTimeOffset RetryAt { get; } = retryAt;
}

public sealed class TranslationClient(HttpClient http)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(string From, string To, string Text), string> cache = [];
    private DateTimeOffset retryAt;

    public static string BrowserUrl(string from, string to, string text) => "https://translate.google.com/?sl=" + Uri.EscapeDataString(from) + "&tl=" + Uri.EscapeDataString(to) + "&text=" + Uri.EscapeDataString(text) + "&op=translate";

    public async Task<string> TranslateAsync(string text, string from, string to, CancellationToken token)
    {
        if (text.Length > 4500) throw new ArgumentException("Mỗi lần dịch tối đa 4500 ký tự. Chọn đoạn ngắn hơn.");
        if (string.IsNullOrWhiteSpace(text) || from == to) return text;
        await gate.WaitAsync(token);
        try
        {
            var key = (from, to, text);
            if (cache.TryGetValue(key, out var cached)) return cached;
            if (DateTimeOffset.UtcNow < retryAt) throw new TranslationRateLimitException(retryAt);
            var url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&sl=" + Uri.EscapeDataString(from) + "&tl=" + Uri.EscapeDataString(to);
            using var body = new FormUrlEncodedContent(new Dictionary<string, string> { ["q"] = text });
            using var response = await http.PostAsync(url, body, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var now = DateTimeOffset.UtcNow;
                var requested = response.Headers.RetryAfter?.Date ?? now.Add(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5));
                retryAt = requested > now.AddSeconds(10) ? requested : now.AddSeconds(10);
                throw new TranslationRateLimitException(retryAt);
            }
            response.EnsureSuccessStatusCode();
            var translated = TranslationText.ReadGoogle(await response.Content.ReadAsStringAsync(token));
            if (cache.Count >= 100) cache.Clear();
            cache[key] = translated;
            return translated;
        }
        finally { gate.Release(); }
    }
}
