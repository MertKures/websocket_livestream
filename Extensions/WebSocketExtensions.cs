using System.Net.WebSockets;

public static class WebSocketExtensions
{
    public static async Task<Result> TrySendAsync(this WebSocket webSocket, ArraySegment<byte> message, WebSocketMessageType type, bool endOfMessage, CancellationToken token)
    {
        if (webSocket.CloseStatus != default || webSocket.CloseStatusDescription != default || webSocket.State != WebSocketState.Open)
            return new ErrorResult("Websocket kapalı olduğu için mesaj dinlenilemedi.");

        try
        {
            await webSocket.SendAsync(message, type, endOfMessage, token);
            return new SuccessResult();
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex.Message);
        }
    }

    public static async Task<Result> TrySendAsync(this WebSocket webSocket, JsonMessage message, WebSocketMessageType type, bool endOfMessage, CancellationToken token)
    {
        if (webSocket.CloseStatus != default || webSocket.CloseStatusDescription != default || webSocket.State != WebSocketState.Open)
            return new ErrorResult("Websocket kapalı olduğu için mesaj gönderilemedi.");

        try
        {
            DataResult<byte[]> messageDataResult = await message.ToJSONAsync();

            if (!messageDataResult.Success)
                return new ErrorResult(messageDataResult.Message);

            ArraySegment<byte> messageToSend = new(messageDataResult.Data, 0, messageDataResult.Data.Length);

            await webSocket.SendAsync(messageToSend, type, endOfMessage, token);
            return new SuccessResult();
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex.Message);
        }
    }

    public static async Task<DataResult<WebSocketReceiveResult>> TryReceiveAsync(this WebSocket webSocket, ArraySegment<byte> buffer, CancellationToken token)
    {
        try
        {
            return new SuccessDataResult<WebSocketReceiveResult>(await webSocket.ReceiveAsync(buffer, token));
        }
        catch (Exception ex)
        {
            return new ErrorDataResult<WebSocketReceiveResult>(ex.Message);
        }
    }
}
