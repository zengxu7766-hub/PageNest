namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// Exposes a local MCP (Model Context Protocol) server for diary data.
    /// The default implementation (<see cref="NullMcpService"/>) is a no-op, so
    /// hosts that do not ship the HTTP server (Gtk / Server / WebAssembly) stay
    /// unaffected. The real implementation lives in Rcl.Hybird.
    /// </summary>
    public interface IMcpService
    {
        /// <summary>Whether a real MCP server implementation is available in this host.</summary>
        bool IsSupported { get; }

        /// <summary>Whether the HTTP server is currently listening.</summary>
        bool IsRunning { get; }

        /// <summary>The endpoint URL shown in settings, or null when stopped.</summary>
        string? Url { get; }

        /// <summary>Start / stop the server according to the current settings. Never throws.</summary>
        void Apply();
    }
}
