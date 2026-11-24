using System.Net.Sockets;
using System.Text.Json;
using TpRedes.NativeHttp;

string applicationDirectory = AppContext.BaseDirectory;

try
{
    ServerSettings settings = ServerSettings.Load(Path.Combine(applicationDirectory, "config.json"));
    TcpHttpServer server = new TcpHttpServer(settings, applicationDirectory);

    using CancellationTokenSource cancellation = new();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    };

    await server.RunAsync(cancellation.Token);
}
catch (Exception exception) when (
    exception is IOException or InvalidDataException or JsonException or SocketException)
{
    Console.Error.WriteLine($"No se pudo iniciar el servidor: {exception.Message}");
    return 1;
}

return 0;
