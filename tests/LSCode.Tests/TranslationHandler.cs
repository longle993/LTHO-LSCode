using System.Net;

internal sealed class TranslationHandler(bool limited = false) : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? Body { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        Body = await request.Content!.ReadAsStringAsync(cancellationToken);
        var response = new HttpResponseMessage(limited ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
        {
            Content = new StringContent("""[[["Xin chào","Hello"]]]""")
        };
        if (limited) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90));
        return response;
    }
}
