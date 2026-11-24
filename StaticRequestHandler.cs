using System.IO.Compression;
using System.Text;

namespace TpRedes.NativeHttp;

public sealed class StaticRequestHandler
{
    private const string DefaultDocument = "index.html";

    private const string BuiltInNotFoundPage =
        "<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"UTF-8\"><title>404</title></head>" +
        "<body><h1>Error 404</h1><p>El archivo solicitado no existe.</p></body></html>";

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".htm"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".xml"] = "application/xml; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif"
    };

    private static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm", ".css", ".js", ".json", ".txt", ".xml", ".svg"
    };

    private readonly string _rootFolder;
    private readonly string _rootFolderWithSeparator;
    private readonly int _compressionMinBytes;
    private readonly StringComparison _pathComparison;

    public StaticRequestHandler(string rootFolder, int compressionMinBytes)
    {
        _rootFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootFolder));
        _rootFolderWithSeparator = _rootFolder.EndsWith(Path.DirectorySeparatorChar)
            ? _rootFolder
            : _rootFolder + Path.DirectorySeparatorChar;
        _compressionMinBytes = compressionMinBytes;
        _pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    public HttpResponse Handle(HttpRequest request)
    {
        return request.Method switch
        {
            "GET" => ServeFile(request),
            "POST" => HttpResponse.FromText(200, "Datos recibidos correctamente"),
            _ => new HttpResponse(
                405,
                "Method Not Allowed",
                "text/plain; charset=utf-8",
                Encoding.UTF8.GetBytes("Método no permitido."),
                allow: "GET, POST")
        };
    }

    private HttpResponse ServeFile(HttpRequest request)
    {
        string relativePath = request.Path.TrimStart('/');
        if (relativePath.Length == 0 || request.Path.EndsWith('/'))
        {
            relativePath = Path.Combine(relativePath, DefaultDocument);
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(_rootFolder, relativePath));
        }
        catch (ArgumentException)
        {
            return HttpResponse.FromText(404, "El archivo solicitado no existe.");
        }

        if (!fullPath.StartsWith(_rootFolderWithSeparator, _pathComparison))
        {
            return HttpResponse.FromText(403, "Acceso denegado.");
        }

        if (Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(fullPath, DefaultDocument);
        }

        if (!File.Exists(fullPath))
        {
            return BuildNotFound();
        }

        byte[] content = File.ReadAllBytes(fullPath);
        string extension = Path.GetExtension(fullPath);
        string contentType = MimeTypes.TryGetValue(extension, out string? mimeType)
            ? mimeType
            : "application/octet-stream";

        bool shouldCompress = request.AcceptsGzip
            && CompressibleExtensions.Contains(extension)
            && content.Length >= _compressionMinBytes;
        if (shouldCompress)
        {
            byte[] compressed = Compress(content);
            if (compressed.Length < content.Length)
            {
                return new HttpResponse(200, "OK", contentType, compressed, isGzip: true);
            }
        }

        return new HttpResponse(200, "OK", contentType, content);
    }

    private HttpResponse BuildNotFound()
    {
        string customPage = Path.Combine(_rootFolder, "404.html");
        byte[] body = File.Exists(customPage)
            ? File.ReadAllBytes(customPage)
            : Encoding.UTF8.GetBytes(BuiltInNotFoundPage);

        return new HttpResponse(404, "Not Found", "text/html; charset=utf-8", body);
    }

    private static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(content, 0, content.Length);
        }

        return output.ToArray();
    }
}
