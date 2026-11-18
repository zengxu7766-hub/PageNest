using Android.App;
using Android.Content;
using Android.Content.PM;

namespace SwashbucklerDiary.Maui.Services
{
    /// <summary>
    /// Foreground service that keeps the app process alive while the MCP HTTP
    /// server is enabled. It is started from <see cref="McpKeepAlive.Start"/> and
    /// only raises the process priority + shows a persistent notification — the
    /// MCP server itself stays a singleton in the main process (see McpService).
    /// </summary>
    [Service(
        Label = "@string/app_name",
        Exported = false,
        ForegroundServiceType = ForegroundServiceType.SpecialUse)]
    public sealed class McpForegroundService : Service
    {
        public const int NotificationId = 20241115;
        public const string ChannelId = "qiye_mcp_keepalive";

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            CreateNotificationChannel();
            var notification = BuildNotification();

            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                // Android 14+ requires the foreground service type to be
                // declared (manifest) and passed when starting.
                StartForeground(NotificationId, notification, ForegroundServiceType.SpecialUse);
            }
            else
            {
                StartForeground(NotificationId, notification);
            }

            // Re-deliver the intent if the system kills the service under memory
            // pressure; the process priority is what we care about here.
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            StopForeground(StopForegroundFlags.Remove);
            base.OnDestroy();
        }

        private void CreateNotificationChannel()
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26))
                return;

            var channel = new NotificationChannel(
                ChannelId,
                GetString(Resource.String.mcp_keepalive_channel),
                NotificationImportance.Low)
            {
                Description = GetString(Resource.String.mcp_keepalive_channel_desc)
            };
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }

        private Notification BuildNotification()
        {
            var openApp = new Intent(this, typeof(MainActivity));
            openApp.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            var pending = PendingIntent.GetActivity(
                this, 0, openApp,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            return new Notification.Builder(this, ChannelId)
                .SetContentTitle(GetString(Resource.String.mcp_keepalive_title))
                .SetContentText(GetString(Resource.String.mcp_keepalive_text))
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentIntent(pending)
                .SetOngoing(true)
                .SetCategory(Notification.CategoryService)
                .Build();
        }
    }
}
