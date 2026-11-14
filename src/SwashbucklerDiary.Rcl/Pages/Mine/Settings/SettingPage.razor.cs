using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Services;

namespace SwashbucklerDiary.Rcl.Pages
{
    public partial class SettingPage : ImportantComponentBase
    {
        private bool showClearCache;

        private bool showStatisticsCard;

        private bool showQuickRecord;

        private string? cacheSize;

        // ---- MCP server ----
        private bool showMcpDialog;
        private bool mcpEnabled;
        private bool mcpAllowLan;
        private string mcpPort = "0";
        private string mcpAuthToken = string.Empty;

        [Inject]
        private IAppFileSystem AppFileSystem { get; set; } = default!;

        [Inject]
        private IMcpService McpService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            await UpdateCacheSize();
        }

        protected override void ReadSettings()
        {
            base.ReadSettings();

            showStatisticsCard = SettingService.Get(s => s.StatisticsCard);
            showQuickRecord = SettingService.Get(s => s.QuickRecord);
            mcpEnabled = SettingService.Get(s => s.McpEnabled);
            mcpAllowLan = SettingService.Get(s => s.McpAllowLan);
            mcpPort = SettingService.Get(s => s.McpPort).ToString();
            mcpAuthToken = SettingService.Get(s => s.McpAuthToken) ?? string.Empty;
        }

        private async Task UpdateCacheSize()
        {
            cacheSize = await AppFileSystem.GetCacheSizeAsync();
        }

        private async Task ClearCache()
        {
            showClearCache = false;
            await AppFileSystem.ClearCacheAsync();
            await UpdateCacheSize();
            await AlertService.SuccessAsync(I18n.T("Clean up successfully"));
        }

        // SettingSwitch with OnChange does NOT persist by itself, so we do it here
        // and then ask the service to (re)start.
        private async Task OnMcpEnabledChanged((string key, bool value) args)
        {
            await SettingService.SetAsync(args.key, args.value);
            McpService.Apply();
        }

        private async Task OnMcpAllowLanChanged((string key, bool value) args)
        {
            await SettingService.SetAsync(args.key, args.value);
            McpService.Apply();
        }

        private async Task SaveMcpSettings()
        {
            int port = int.TryParse(mcpPort, out var p) ? p : 0;
            await SettingService.SetAsync(s => s.McpPort, port);
            await SettingService.SetAsync(s => s.McpAuthToken, mcpAuthToken ?? string.Empty);
            showMcpDialog = false;
            McpService.Apply();
        }
    }
}
