using System.Globalization;
using System.Text;

namespace TpRedes.NativeHttp;

public sealed class HttpProtocolException : Exception
{
    public int StatusCode { get; }

    public HttpProtocolException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public static class HttpRequestParser
{
    private const int MaxRequestLineBytes = 8192;
    private const int MaxHeaderBytes = 16 * 1024;
    private const int MaxBodyBytes = 1024 * 1024;

    public static async Task<HttpRequest?> ParseAsync(Stream stream, CancellationToken token)
    {
        string? requestLine = await ReadLineAsync(stream, MaxRequestLineBytes, token);
        if (requestLine is null)
        {
            return null;
        }

        string[] requestParts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (requestParts.Length != 3 ||
            (requestParts[2] != "HTTP/1.0" && requestParts[2] != "HTTP/1.1"))
        {
            throw new HttpProtocolException(400, "Línea de solicitud inválida.");
        }

        string method = requestParts[0].ToUpperInvariant();
        string target = requestParts[1];
        if (!target.StartsWith('/'))
        {
            throw new HttpProtocolException(400, "La ruta debe comenzar con '/'.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int headerBytes = requestLine.Length + 2;
        while (true)
        {
            string? line = await ReadLineAsync(stream, MaxHeaderBytes, token);
            if (line is null)
            {
                throw new HttpProtocolException(400, "Solicitud incompleta.");
            }

            headerBytes += line.Length + 2;
            if (headerBytes > MaxHeaderBytes)
            {
                throw new HttpProtocolException(431, "Los encabezados son demasiado grandes.");
            }

            if (line.Length == 0)
            {
                break;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                throw new HttpProtocolException(400, "Encabezado HTTP inválido.");
            }

            string name = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();
            if (headers.ContainsKey(name))
            {
                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    throw new HttpProtocolException(400, "Content-Length duplicado.");
                }

                headers[name] += "," + value;
            }
            else
            {
                headers.Add(name, value);
            }
        }

        if (headers.ContainsKey("Transfer-Encoding"))
        {
            throw new HttpProtocolException(501, "Transfer-Encoding no está soportado.");
        }

        byte[] body = await ReadBodyAsync(stream, headers, token);
        int queryIndex = target.IndexOf('?');
        string rawPath = queryIndex >= 0 ? target[..queryIndex] : target;
        string query = queryIndex >= 0 ? target[(queryIndex + 1)..] : string.Empty;

        string path;
        try
        {
            path = Uri.UnescapeDataString(rawPath);
        }
        catch (UriFormatException)
        {
            throw new HttpProtocolException(400, "La ruta contiene una codificación inválida.");
        }

        if (path.Contains('\0'))
        {
            throw new HttpProtocolException(400, "La ruta contiene caracteres inválidos.");
        }

        return new HttpRequest(method, path, query, headers, Encoding.UTF8.GetString(body));
    }

    private static async Task<byte[]> ReadBodyAsync(
        Stream stream,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken token)
    {
        if (!headers.TryGetValue("Content-Length", out string? value))
        {
            return Array.Empty<byte>();
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int contentLength) ||
            contentLength < 0)
        {
            throw new HttpProtocolException(400, "Content-Length inválido.");
        }

        if (contentLength > MaxBodyBytes)
        {
            throw new HttpProtocolException(413, "El cuerpo de la solicitud supera 1 MiB.");
        }

        byte[] body = new byte[contentLength];
        int totalRead = 0;
        while (totalRead < body.Length)
        {
            int read = await stream.ReadAsync(body.AsMemory(totalRead), token);
            if (read == 0)
            {
                throw new HttpProtocolException(400, "El cuerpo de la solicitud llegó incompleto.");
            }

            totalRead += read;
        }

        return body;
    }

    private static async Task<string?> ReadLineAsync(
        Stream stream,
        int maxLineBytes,
        CancellationToken token)
    {
        var bytes = new List<byte>();
        byte[] singleByte = new byte[1];

        while (true)
        {
            int read = await stream.ReadAsync(singleByte.AsMemory(), token);
            if (read == 0)
            {
                if (bytes.Count == 0)
                {
                    return null;
                }

                throw new HttpProtocolException(400, "Línea HTTP incompleta.");
            }

            if (singleByte[0] == (byte)'\n')
            {
                if (bytes.Count == 0 || bytes[^1] != (byte)'\r')
                {
                    throw new HttpProtocolException(400, "Las líneas HTTP deben terminar con CRLF.");
                }

                bytes.RemoveAt(bytes.Count - 1);
                return Encoding.Latin1.GetString(bytes.ToArray());
            }

            bytes.Add(singleByte[0]);
            if (bytes.Count > maxLineBytes)
            {
                throw new HttpProtocolException(431, "Línea HTTP demasiado larga.");
            }
        }
    }
}
