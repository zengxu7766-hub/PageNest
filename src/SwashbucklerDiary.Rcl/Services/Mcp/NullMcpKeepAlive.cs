namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// No-op keep-alive for hosts that do not ship a foreground service
    /// (Gtk / Server / WebAssembly). Keeps McpService's constructor satisfiable everywhere.
    /// </summary>
    public sealed class NullMcpKeepAlive : IMcpKeepAlive
    {
        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
