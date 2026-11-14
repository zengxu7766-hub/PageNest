using EmbedIO;
using EmbedIO.WebApi;
using SwashbucklerDiary.Rcl.Hybird.Extensions;
using SwashbucklerDiary.Rcl.Services;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SwashbucklerDiary.Rcl.Hybird.Services
{
    /// <summary>
    /// Owns the MCP HTTP server lifecycle. Registered as a singleton in the
    /// Maui host (the only host that ships Rcl.Hybird). Call <see cref="Apply"/>
    /// at startup and whenever the MCP settings change.
    /// </summary>
    public sealed class McpService : IMcpService, IDisposable
    {
        private readonly ISettingService _settingService;
        private readonly IDiaryService _diaryService;
        private readonly ITagService _tagService;
        private readonly object _sync = new();

        private WebServer? _server;
        private McpContext? _context;

        public McpService(ISettingService settingService, IDiaryService diaryService, ITagService tagService)
        {
            _settingService = settingService;
            _diaryService = diaryService;
            _tagService = tagService;
        }

        public bool IsSupported => true;

        public bool IsRunning
        {
            get { lock (_sync) return _server is not null; }
        }

        public string? Url { get; private set; }

        /// <summary>Start / stop according to current settings. Never throws.</summary>
        public void Apply()
        {
            try
            {
                bool enabled = _settingService.Get(s => s.McpEnabled);
                if (!enabled)
                {
                    Stop();
                    return;
                }

                // Restarting unconditionally also picks up port / token / LAN changes.
                Stop();
                Start();
            }
            catch
            {
                // A failure here must never prevent the app from starting.
                Stop();
            }
        }

        private void Start()
        {
            int port = _settingService.Get(s => s.McpPort);
            if (port <= 0 || port > 65535)
            {
                port = IPAddress.Loopback.GetAvailablePort();
            }

            bool allowLan = _settingService.Get(s => s.McpAllowLan);
            var host = allowLan ? "*" : "127.0.0.1";
            var token = _settingService.Get(s => s.McpAuthToken) ?? string.Empty;
            var baseUrl = $"http://{host}:{port}/";

            var context = new McpContext
            {
                Token = token,
                Tools = new McpTools(_diaryService, _tagService, _settingService)
            };

            var server = new WebServer(o => o.WithUrlPrefix(baseUrl))
                .WithWebApi("/", m => m.WithController(() => new McpController(context)));

            server.RunAsync();

            lock (_sync)
            {
                _server = server;
                _context = context;
            }
            // "0.0.0.0" is a wildcard, not a connectable address: show a real
            // LAN IPv4 so the URL can be pasted into an MCP client.
            var displayHost = allowLan ? (GetLanIPv4() ?? "127.0.0.1") : "127.0.0.1";
            Url = $"http://{displayHost}:{port}/mcp";
        }

        /// <summary>Best-effort first non-loopback IPv4 of an operational interface.</summary>
        private static string? GetLanIPv4()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
                    {
                        var addr = unicast.Address;
                        if (addr.AddressFamily == AddressFamily.InterNetwork
                            && !IPAddress.IsLoopback(addr))
                        {
                            return addr.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Network information is advisory only.
            }
            return null;
        }

        private void Stop()
        {
            WebServer? server;
            lock (_sync)
            {
                server = _server;
                _server = null;
                _context = null;
            }
            server?.Dispose();
            Url = null;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }
    }
}
