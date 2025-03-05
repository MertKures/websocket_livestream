using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Serilog;
using Serilog.Core;
using System.Buffers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ILogger = Microsoft.Extensions.Logging.ILogger;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add WebSocket support
builder.Services.AddWebSockets(configure: options =>
{
    var configuration = builder.Configuration;
    //options.AllowedOrigins.Add(configuration.GetValue<string>("BASE_URL"));
    options.AllowedOrigins.Add("*");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowOrigin",
        //configurePolicy: builder => builder.WithOrigins("https://localhost:5173")
        //configurePolicy: builder => builder.WithOrigins(Configuration.GetValue<string>("BASE_URL"))
        configurePolicy: builder => builder.WithOrigins("*")
    );
});

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("logs.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(builder => builder.WithOrigins("*").AllowAnyHeader());
app.UseHttpsRedirection();
app.UseWebSockets();

List<WebSocket> subscribers = new();

bool streaming = false;
StringBuilder latest_base64_image_sb = new();

async Task ReceiveAsync(ILogger logger, WebSocket webSocket, ArraySegment<byte> buffer, CancellationToken token)
{
    do
    {
        DataResult<WebSocketReceiveResult> receiveDataResult = await webSocket.TryReceiveAsync(buffer, token);

        if (!receiveDataResult.Success)
        {
            logger.LogError(receiveDataResult.Message);
            continue;
        }

        await CheckSocketStatus(logger, webSocket, receiveDataResult.Data);

        await HandleMessage(logger, webSocket, buffer.Array, receiveDataResult.Data, token);
    } while (webSocket.State == WebSocketState.Open);

    logger.LogInformation(
        string.Format(
            "{0}. State: {1}, CloseStatus: {2}",
            "WebSocket bağlantısı sona erdi",
            webSocket.State,
            (webSocket.CloseStatus == null) ? "-" : webSocket.CloseStatus
        )
    );
}

async Task<bool> CheckSocketStatus(ILogger logger, WebSocket webSocket, WebSocketReceiveResult receiveResult)
{
    if (receiveResult.CloseStatus.HasValue || receiveResult.MessageType == WebSocketMessageType.Close)
    {
        try
        {
            logger.LogInformation("Bağlantı karşı taraftan kapatıldı.");
            await webSocket.CloseAsync(receiveResult.CloseStatus ?? WebSocketCloseStatus.NormalClosure, receiveResult.CloseStatusDescription, new CancellationTokenSource(10000).Token);
            logger.LogInformation("Sunucu tarafındaki bağlantı da kapatıldı.");
        }
        catch (Exception ex) { logger.LogError(ex.Message); }

        return false;
    }
    else if (webSocket.State != WebSocketState.Open)
        return false;
    return true;
}

async Task HandleMessage(ILogger logger, WebSocket webSocket, byte[] buffer, WebSocketReceiveResult receiveResult, CancellationToken token)
{
    if (receiveResult.MessageType != WebSocketMessageType.Text)
    {
        logger.LogInformation("Metin olmayan veriler geldi. Bu mesajlar işlenmeyecek.");
        return;
    }

    // TODO: Check this buffer size.
    using MemoryStream memoryStream = new(1 * 1024 * 1024);

    await memoryStream.WriteAsync(buffer, 0, receiveResult.Count);

    if (!receiveResult.EndOfMessage)
    {
        DataResult<WebSocketReceiveResult> receiveDataResult;

        do
        {
            // TODO: Buffer içerisine veri almak yerine MemoryStream içerisine veri almak.

            receiveDataResult = await webSocket.TryReceiveAsync(buffer, token);

            if (!receiveDataResult.Success)
            {
                logger.LogError(receiveDataResult.Message);
                return;
            }

            await CheckSocketStatus(logger, webSocket, receiveDataResult.Data);

            await memoryStream.WriteAsync(buffer, 0, receiveDataResult.Data.Count);
        } while (!receiveDataResult.Data.EndOfMessage);
    }

    JsonMessage receivedMessage = new();

    Result receivedMessageResult = await receivedMessage.LoadJSONAsync(memoryStream.ToArray(), 0, (int)memoryStream.Position);

    if (!receivedMessageResult.Success)
    {
        await webSocket.TrySendAsync(new JsonMessage()
                {
                    { "status", "ERROR" },
                    { "message", receivedMessageResult.Message }
                }, WebSocketMessageType.Text, true, CancellationToken.None);

        logger.LogError(receivedMessageResult.Message);
        return;
    }

    await HandleJsonMessage(logger, webSocket, receivedMessage);
}

async Task HandleJsonMessage(ILogger logger, WebSocket webSocket, JsonMessage receivedMessage)
{
    if (!receivedMessage.ContainsKey("type"))
    {
        logger.LogWarning("Bir json mesajı 'type' özelliğine sahip olmadığından işlenmedi.");
        return;
    }

    JsonMessage response = new();

    ImageStreamServiceMessageTypes messageType;
    if (receivedMessage.TryGetValue("type", out object? _messageTypeObj))
    {
        string _messageType = (_messageTypeObj?.ToString() ?? string.Empty).ToUpperInvariant();

        if (string.IsNullOrEmpty(_messageType))
        {
            logger.LogWarning("Mesajın 'type' özelliği boş olduğundan işlenemedi.");
            return;
        }
        else if (!Enum.TryParse(_messageType, out messageType))
        {
            logger.LogWarning("Mesajın 'type' özelliği geçersiz olduğundan mesaj işlenemedi.");
            return;
        }

        logger.LogInformation(string.Format("Mesajın 'type' özelliği '{0}' olarak alındı.", Enum.GetName(messageType)));
    }
    else
    {
        logger.LogWarning("Mesajın 'type' özelliği alınamadığından mesaj işlenemedi.");
        return;
    }

    switch (messageType)
    {
        case ImageStreamServiceMessageTypes.PING:
            response.Add("status", "OK");
            response.Add("message", "pong");
            break;
        case ImageStreamServiceMessageTypes.IMAGE:
            response.Add("status", "OK");
            response.Add("type", "IMAGE");
            response.Add("message", "Image received.");

            receivedMessage.TryGetValue("image", out object? imageBase64Obj);
            string imageBase64 = imageBase64Obj?.ToString() ?? string.Empty;

            NetHuffman.Tree tree = new NetHuffman.Tree();

            tree.BuildDictionary("this is just a test");

            NetHuffman.Coder coder = new NetHuffman.Coder(tree);

            byte[] encoded;
            uint size = coder.Encode(imageBase64, out encoded);

            logger.LogInformation("Image size after Huffman Encoding: {0}", encoded.Length);

            // TODO: Create an abstract function for sending large data.
            subscribers.ForEach(async subscriber =>
            {
                if (subscriber.State == WebSocketState.Open)
                {
                    Result sendResult = await subscriber.TrySendAsync(receivedMessage, WebSocketMessageType.Text, true, CancellationToken.None);

                    if (!sendResult.Success)
                        logger.LogError(sendResult.Message);
                }
            });
            break;
        default:
            response.Add("status", "ERROR");
            response.Add("message", "Geçerli bir 'type' özelliği gönderilmek zorundadır.");
            return;
    }

    if (!response.ContainsKey("status"))
    {
        logger.LogWarning("Gelen mesaja karşılık herhangi bir 'status' özelliği gönderilmediğinden mesaj gönderilmedi.");

        response.Clear();
        response.Add("status", "ERROR");
        response.Add("message", "Mesaj, sunucu tarafından doğru bir şekilde işlenemedi.");
    }

    Result sendResult = await webSocket.TrySendAsync(response, WebSocketMessageType.Text, true, CancellationToken.None);

    if (!sendResult.Success)
    {
        logger.LogError(sendResult.Message);
        return;
    }
}


app.MapGet("/api/stream/ws", async (HttpContext context, ILoggerFactory loggerFactory) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var logger = loggerFactory.CreateLogger("Start");
    logger.LogInformation("Starting...");

    #region Query Parameters

    ImageStreamWebSocketTypes socketType;

    if (context.Request.Query.TryGetValue("socket_type", out StringValues socketTypes))
    {
        bool intConversionState = int.TryParse(socketTypes[0], out int socketTypeInt);

        if (socketTypes.Count <= 0 || !intConversionState || !Enum.IsDefined((ImageStreamWebSocketTypes)socketTypeInt))
        {
            logger.LogError("Invalid socket type: {0}", socketTypes[0]);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        socketType = (ImageStreamWebSocketTypes)socketTypeInt;

        logger.LogInformation("Socket type: {0}", socketType);
    }
    else
    {
        logger.LogInformation("Socket type not found.");
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    string token = "";

    if (socketType == ImageStreamWebSocketTypes.PUBLISHER)
    {
        if (!context.Request.Query.TryGetValue("token", out StringValues tokenTuple))
        {
            logger.LogInformation("Publisher must have a token.");
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        token = tokenTuple[0];

        // TODO: Query from database.
        if (token != "<token>")
        {
            logger.LogInformation("Publisher token was invalid.");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
    }

    #endregion Query Parameters

    using WebSocket websocket = await context.WebSockets.AcceptWebSocketAsync();

    byte[] bytes = ArrayPool<byte>.Shared.Rent(WebSocketBufferConstants.Wifi);

    ArraySegment<byte> buffer = new(bytes);

    if (buffer.Array == null)
    {
        logger.LogError(ImageStreamMessages.CouldNotCreateBuffer);

        Result sendResult = await websocket.TrySendAsync(new JsonMessage()
                {
                    { "status", "ERROR" },
                    { "message", ImageStreamMessages.CouldNotCreateBuffer }
                }, WebSocketMessageType.Text, true, new CancellationTokenSource(2000).Token);

        if (!sendResult.Success)
            logger.LogError(sendResult.Message);

        try
        {
            await websocket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, ImageStreamMessages.InternalServerError, new CancellationTokenSource(5000).Token);
        }
        catch (Exception ex) { logger.LogError(ex.Message); }

        ArrayPool<byte>.Shared.Return(bytes);
        websocket.Dispose();

        return;
    }

    try
    {
        if (socketType == ImageStreamWebSocketTypes.PUBLISHER)
        {
            if (streaming)
            {
                logger.LogInformation("There is already a streaming connection.");
                await websocket.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, ImageStreamMessages.PolicyViolation, new CancellationTokenSource(5000).Token);
                return;
            }

            streaming = true;
            await ReceiveAsync(logger, websocket, buffer, CancellationToken.None);
        }
        else if (socketType == ImageStreamWebSocketTypes.SUBSCRIBER)
        {
            subscribers.Add(websocket);
            await ReceiveAsync(logger, websocket, buffer, CancellationToken.None);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex.Message);

        Result sendResult = await websocket.TrySendAsync(new JsonMessage()
                {
                    { "status", "ERROR" },
                    { "message", ex.Message }
                }, WebSocketMessageType.Text, true, new CancellationTokenSource(2000).Token);

        if (!sendResult.Success)
            logger.LogError(sendResult.Message);

        try
        {
            await websocket.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, ImageStreamMessages.InternalServerError, new CancellationTokenSource(5000).Token);
        }
        catch (Exception ex2) { logger.LogError(ex2.Message); }
    }
    finally
    {
        if (socketType == ImageStreamWebSocketTypes.PUBLISHER)
            streaming = false;
        else if (socketType == ImageStreamWebSocketTypes.SUBSCRIBER)
            subscribers.Remove(websocket);
    }

    ArrayPool<byte>.Shared.Return(bytes);
    websocket.Dispose();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();