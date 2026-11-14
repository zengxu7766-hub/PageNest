using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Components;
using SwashbucklerDiary.Rcl.Models;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Pages
{
    public partial class AboutPage : ImportantComponentBase
    {
        private bool showSourceCode;

        private List<DynamicListItem> codeSourceListItems = [];

        private List<List<DynamicListItem>> viewLists = [];

        [Inject]
        private IVersionUpdataManager VersionUpdataManager { get; set; } = default!;

        protected override void OnInitialized()
        {
            base.OnInitialized();

            LoadView();
        }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            await LoadViewAsync();
        }

        private string AppVersion => PlatformIntegration.AppVersionString;

        private void LoadView()
        {
            viewLists =
            [
                [
                    new(this, "View source","auto_stories",() => showSourceCode = true),
                    new(this, "User agreement","mdi:mdi-file-document-multiple-outline",() => To("user-agreement")),
                    new(this, "Privacy policy","lock",() => To("privacy-policy")),
                    new(this, "Check for updates","update",CheckForUpdates),
                ],
                [
                    new(this, "Open source related","mdi:mdi-xml",() => To("relatedOSP")),
                    new(this, "Log file","mdi:mdi-file-document-edit-outline",() => To("log")),
                ]
            ];
        }

        private async Task LoadViewAsync()
        {
            var codeSources = await StaticWebAssets.ReadRclJsonAsync<List<CodeSource>>("json/code-source/code-source.json");
            codeSourceListItems = codeSources.Select(it => new DynamicListItem(this, it.Name ?? string.Empty, it.Icon ?? string.Empty, () => ViewSourceCode(it.Url))).ToList();
        }

        private async Task ViewSourceCode(string? url)
        {
            await PlatformIntegration.OpenBrowser(url);
            await HandleAchievements(Achievement.SourceCode);
        }

        private void CheckForUpdates()
        {
            VersionUpdataManager.CheckUpdates();
        }
    }
}
