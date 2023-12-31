internal sealed class Settings
{
    internal sealed class UpstreamOptions
    {
        internal sealed class DnsOptions
        {
            /// <summary>
            /// Upstream resolver address
            /// </summary>
            public string Address { get; set; } = "1.1.1.1";

            /// <summary>
            /// Upstream resolver timeout
            /// </summary>
            public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
        }

        internal sealed class HttpOptions
        {
            /// <summary>
            /// <see cref="HttpClientFactoryOptions.HandlerLifetime" />
            /// </summary>
            public TimeSpan Handler { get; set; } = TimeSpan.FromMinutes(2);

            /// <summary>
            /// <see cref="HttpClient.Timeout"/>
            /// </summary>
            public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);
        }

        public DnsOptions Dns { get; set; } = new();
        public HttpOptions Http { get; set; } = new();
    }

    internal sealed class TTLOptions
    {
        /// <summary>
        /// TTL for error, timeout and nonexistent domains
        /// </summary>
        public TimeSpan Negative { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// TTL for passing, proxying domains
        /// </summary>
        public TimeSpan Positive { get; set; } = TimeSpan.FromMinutes(1);
    }

    public bool Promiscuous { get; set; }
    public UpstreamOptions Upstream { get; set; } = new();
    public TTLOptions TTL { get; set; } = new();
    public IReadOnlyDictionary<Domain, Behavior> Rules { get; set; } = new Dictionary<Domain, Behavior>();
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Subs { get; set; }
}
