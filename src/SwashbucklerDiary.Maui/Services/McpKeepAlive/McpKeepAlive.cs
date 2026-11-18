using Android.Content;

namespace SwashbucklerDiary.Maui.Services
{
    /// <summary>
    /// Android implementation of <see cref="Rcl.Services.IMcpKeepAlive"/>.
    /// Starts / stops <see cref="McpForegroundService"/> so the app process
    /// (which hosts the MCP HTTP server) is treated as a foreground process.
    /// </summary>
    public sealed class McpKeepAlive : Rcl.Services.IMcpKeepAlive
    {
        private static Context AppContext => Microsoft.Maui.ApplicationModel.Platform.AppContext;

        public void Start()
        {
            var context = AppContext;
            var intent = new Intent(context, typeof(McpForegroundService));
            context.StartForegroundService(intent);
        }

        public void Stop()
        {
            var context = AppContext;
            var intent = new Intent(context, typeof(McpForegroundService));
            context.StopService(intent);
        }
    }
}
