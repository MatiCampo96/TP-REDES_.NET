namespace TpRedes.NativeHttp;

public sealed class HttpRequest
{
    public string Method { get; }
    public string Path { get; }
    public string Query { get; }
    public IReadOnlyDictionary<string, string> Headers { get; }
    public string Body { get; }

    public HttpRequest(
        string method,
        string path,
        string query,
        IReadOnlyDictionary<string, string> headers,
        string body)
    {
        Method = method;
        Path = path;
        Query = query;
        Headers = headers;
        Body = body;
    }

    public bool AcceptsGzip
    {
        get
        {
            if (!Headers.TryGetValue("Accept-Encoding", out string? value))
            {
                return false;
            }

            foreach (string encoding in value.Split(','))
            {
                string[] parts = encoding.Trim().Split(';', 2);
                if (!parts[0].Trim().Equals("gzip", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (parts.Length == 1)
                {
                    return true;
                }

                string quality = parts[1].Trim();
                if (!quality.StartsWith("q=", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return double.TryParse(
                           quality[2..],
                           System.Globalization.NumberStyles.AllowDecimalPoint,
                           System.Globalization.CultureInfo.InvariantCulture,
                           out double q) && q > 0;
            }

            return false;
        }
    }
}
