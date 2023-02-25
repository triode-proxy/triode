internal sealed class PubSub<T>
{
    private readonly ConcurrentDictionary<object, Channel<T>> _channels = new();

    public void Send(T message)
    {
        foreach (var channel in _channels.Values)
            Ensure(channel.Writer.TryWrite(message));
    }

    public Subscriber Subscribe()
    {
        var id = new object();
        var channel = Channel.CreateUnbounded<T>(new() { SingleReader = true });
        Ensure(_channels.TryAdd(id, channel));
        return new(_channels, id, channel);
    }

    public sealed class Subscriber : IDisposable
    {
        private readonly ConcurrentDictionary<object, Channel<T>> _channels;
        private readonly object _id;
        private readonly Channel<T> _channel;

        internal Subscriber(ConcurrentDictionary<object, Channel<T>> channels, object id, Channel<T> channel) =>
            (_channels, _id, _channel) = (channels, id, channel);

        public void Dispose() =>
            Ensure(_channels.TryRemove(_id, out _));

        public ValueTask<T> ReceiveAsync(CancellationToken cancellationToken) =>
            _channel.Reader.ReadAsync(cancellationToken);
    }

    private static void Ensure(bool succeeded) => Debug.Assert(succeeded);
}
