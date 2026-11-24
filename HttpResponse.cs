using System.Text;

namespace TpRedes.NativeHttp;

public sealed class HttpResponse
{
    public int StatusCode { get; }
    public string ReasonPhrase { get; }
    public string ContentType { get; }
    public byte[] Body { get; }
    public bool IsGzip { get; }
    public string? Allow { get; }

    public HttpResponse(
        int statusCode,
        string reasonPhrase,
        string contentType,
        byte[] body,
        bool isGzip = false,
        string? allow = null)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        ContentType = contentType;
        Body = body;
        IsGzip = isGzip;
        Allow = allow;
    }

    public static HttpResponse FromText(int statusCode, string message)
    {
        return new HttpResponse(
            statusCode,
            ReasonFor(statusCode),
            "text/plain; charset=utf-8",
            Encoding.UTF8.GetBytes(message));
    }

    public async Task WriteToAsync(Stream stream, CancellationToken token)
    {
        var headers = new StringBuilder()
            .Append("HTTP/1.1 ").Append(StatusCode).Append(' ').Append(ReasonPhrase).Append("\r\n")
            .Append("Content-Type: ").Append(ContentType).Append("\r\n")
            .Append("Content-Length: ").Append(Body.Length).Append("\r\n");

        if (IsGzip)
        {
            headers.Append("Content-Encoding: gzip\r\n");
        }

        headers.Append("Vary: Accept-Encoding\r\n");
        if (Allow is not null)
        {
            headers.Append("Allow: ").Append(Allow).Append("\r\n");
        }

        headers.Append("Connection: close\r\n\r\n");
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers.ToString());
        await stream.WriteAsync(headerBytes, token);
        await stream.WriteAsync(Body, token);
        await stream.FlushAsync(token);
    }

    private static string ReasonFor(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            413 => "Payload Too Large",
            431 => "Request Header Fields Too Large",
            500 => "Internal Server Error",
            501 => "Not Implemented",
            _ => "Error"
        };
    }
}
