using System.Net;
using System.Net.Sockets;

namespace TpRedes.NativeHttp;

public sealed class TcpHttpServer
{
    private readonly int _port;
    private readonly StaticRequestHandler _requestHandler;
    private readonly RequestLogger _requestLogger;

    public TcpHttpServer(ServerSettings settings, string applicationDirectory)
    {
        string rootFolder = Path.GetFullPath(settings.RootFolder, applicationDirectory);
        string logFolder = Path.GetFullPath(settings.LogFolder, applicationDirectory);

        if (!Directory.Exists(rootFolder))
        {
            throw new DirectoryNotFoundException($"La carpeta a servir no existe: {rootFolder}");
        }

        _port = settings.Port;
        _requestHandler = new StaticRequestHandler(rootFolder, settings.CompressionMinBytes);
        _requestLogger = new RequestLogger(logFolder);

        Console.WriteLine($"Carpeta a servir: {rootFolder}");
        Console.WriteLine($"Carpeta de logs:  {logFolder}");
    }

    public async Task RunAsync(CancellationToken token)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"Servidor escuchando en http://localhost:{_port} (Ctrl+C para detener)");

        try
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(token);
                _ = HandleClientAsync(client, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Console.WriteLine("Cierre solicitado; deteniendo la escucha.");
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken serverToken)
    {
        string clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "desconocida";

        using (client)
        using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(serverToken))
        {
            linkedCancellation.CancelAfter(TimeSpan.FromSeconds(10));
            using var stream = new BufferedStream(client.GetStream());

            HttpRequest? request = null;
            HttpResponse response;
            try
            {
                request = await HttpRequestParser.ParseAsync(stream, linkedCancellation.Token);
                if (request is null)
                {
                    return;
                }

                response = _requestHandler.Handle(request);
            }
            catch (HttpProtocolException exception)
            {
                response = HttpResponse.FromText(exception.StatusCode, exception.Message);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                Console.WriteLine($"[{clientIp}] Solicitud cancelada o vencida.");
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Console.Error.WriteLine($"[{clientIp}] Error procesando la solicitud: {exception.Message}");
                response = HttpResponse.FromText(500, "Error interno del servidor.");
            }

            try
            {
                _requestLogger.Log(clientIp, request, response);
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"No se pudo escribir el log: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine($"No hay permisos para escribir el log: {exception.Message}");
            }

            try
            {
                await response.WriteToAsync(stream, linkedCancellation.Token);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                Console.WriteLine($"[{clientIp}] Conexión cancelada durante la respuesta.");
            }
            catch (IOException exception)
            {
                Console.WriteLine($"[{clientIp}] Conexión interrumpida: {exception.Message}");
            }
        }
    }
}
