using System.Collections.Concurrent;
using System.Net.WebSockets;

public sealed class ImageStreamWebSocketManager
{
    public static readonly object _lock = new object();

    private static readonly Lazy<ImageStreamWebSocketManager> instance = new Lazy<ImageStreamWebSocketManager>(valueFactory: () => new ImageStreamWebSocketManager());

    private ImageStreamWebSocketManager() { }

    public static ImageStreamWebSocketManager Instance
    {
        get => instance.Value;
    }

    public static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(initialCount: 1, maxCount: 1);

    /// <summary>
    /// string: User email,
    /// Dictionary<string, ValueTuple<WebSocket, List<WebSocket>>>: Drone ID, (Publisher, Subscribers)
    /// </summary>
    public ConcurrentDictionary<string, Dictionary<int, ValueTuple<WebSocket, List<WebSocket>>>> PublisherSubcriberPairs = new ConcurrentDictionary<string, Dictionary<int, ValueTuple<WebSocket, List<WebSocket>>>>();
}
