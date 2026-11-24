# Servidor web nativo en C#/.NET

Servidor HTTP/1.1 implementado con `TcpListener`, `NetworkStream` y un parser propio, sin ASP.NET ni Kestrel. El proyecto sigue los requisitos del TP.

## Ejecutar

Requiere el SDK de .NET 8. Desde esta carpeta:

```powershell
dotnet run
```

La configuración se lee de `config.json`. Por defecto, el servidor escucha en <http://localhost:8080>, sirve archivos de `www` y escribe los logs diarios en `logs`. Ctrl+C cancela la espera de nuevas conexiones.

## Estructura

| Archivo | Responsabilidad |
|---|---|
| `Program.cs` | Configuración inicial y señal de cierre |
| `ServerSettings.cs` | Carga y validación de `config.json` |
| `TcpHttpServer.cs` | `TcpListener` y una tarea asíncrona por conexión |
| `HttpRequest.cs` / `HttpRequestParser.cs` | Modelo de petición y lectura del protocolo |
| `HttpResponse.cs` | Construcción y escritura de respuestas |
| `StaticRequestHandler.cs` | GET/POST, archivos, 404 y compresión |
| `RequestLogger.cs` | Registro diario sincronizado |

## Probar

```powershell
curl.exe -i http://localhost:8080/
curl.exe -i "http://localhost:8080/?nombre=Ana&edad=20"
curl.exe -i http://localhost:8080/archivo-inexistente.html
curl.exe -i -X POST http://localhost:8080/ -H "Content-Type: application/x-www-form-urlencoded" --data "nombre=Ana&edad=20"
curl.exe -s -D - -o NUL -H "Accept-Encoding: gzip" http://localhost:8080/
```

## Cómo cumple la consigna

- Atiende conexiones concurrentes mediante operaciones de socket asíncronas; no fija un máximo artificial.
- Sirve `index.html` para `/` y para directorios.
- Lee el puerto y las carpetas desde `config.json`.
- Devuelve `404` con la página `www/404.html` cuando no encuentra un archivo.
- Acepta GET y POST. El POST no procesa los datos: se registran en el log.
- Los parámetros de query se conservan para el log, no se usan para generar la respuesta.
- Comprime con GZIP los archivos de texto que el cliente acepta, si superan el umbral configurado y la compresión reduce su tamaño.
- Registra fecha, IP, método, ruta, query y resultado en un archivo diario. El acceso concurrente al log se protege con `lock`.

## Límites y decisiones adicionales

- Se procesa una solicitud por conexión y luego se envía `Connection: close`.
- Se admite `Content-Length` hasta 1 MiB. `Transfer-Encoding: chunked` se rechaza con `501`.
- Las conexiones inactivas vencen a los 10 segundos; el cuerpo del POST en el log se limita a 2000 caracteres y se escapan sus saltos de línea.
- Se bloquean rutas que escapan de la carpeta configurada, respondiendo `403`.
