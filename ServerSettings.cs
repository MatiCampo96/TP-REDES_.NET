using System.Text.Json;

namespace TpRedes.NativeHttp;

public sealed class ServerSettings
{
    public int Port { get; set; } = 8080;
    public string RootFolder { get; set; } = "www";
    public string LogFolder { get; set; } = "logs";
    public int CompressionMinBytes { get; set; } = 200;

    public static ServerSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No se encontró el archivo de configuración: {path}");
        }

        string json = File.ReadAllText(path);
        ServerSettings? config = JsonSerializer.Deserialize<ServerSettings>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (config is null)
        {
            throw new InvalidDataException("El archivo de configuración está vacío o es inválido.");
        }

        if (config.Port is < 1 or > 65535)
        {
            throw new InvalidDataException($"Puerto inválido en la configuración: {config.Port}");
        }

        if (string.IsNullOrWhiteSpace(config.RootFolder) || string.IsNullOrWhiteSpace(config.LogFolder))
        {
            throw new InvalidDataException("RootFolder y LogFolder deben tener una ruta.");
        }

        if (config.CompressionMinBytes < 0)
        {
            throw new InvalidDataException("CompressionMinBytes no puede ser negativo.");
        }

        return config;
    }
}
