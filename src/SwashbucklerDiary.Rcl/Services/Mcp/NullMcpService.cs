namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// No-op implementation for hosts that do not ship the MCP HTTP server
    /// (Gtk / Server / WebAssembly). Keeps the settings page safe everywhere.
    /// </summary>
    public class NullMcpService : IMcpService
    {
        public bool IsSupported => false;

        public bool IsRunning => false;

        public string? Url => null;

        public void Apply()
        {
        }
    }
}
