namespace SwashbucklerDiary.Rcl.Services
{
    /// <summary>
    /// Platform hook that keeps the process alive while the MCP HTTP server is running.
    /// Default implementation is a no-op; the Android/Maui host supplies a foreground
    /// service implementation so the OS does not reclaim the app process in the background.
    /// </summary>
    public interface IMcpKeepAlive
    {
        /// <summary>Begin keeping the host process alive (e.g. start a foreground service).</summary>
        void Start();

        /// <summary>Stop keeping the host process alive.</summary>
        void Stop();
    }
}
