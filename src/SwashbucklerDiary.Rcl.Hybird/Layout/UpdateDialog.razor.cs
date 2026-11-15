using Microsoft.AspNetCore.Components;
using SwashbucklerDiary.Rcl.Essentials;
using SwashbucklerDiary.Rcl.Extensions;
using SwashbucklerDiary.Rcl.Layout;
using SwashbucklerDiary.Rcl.Models;
using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;

namespace SwashbucklerDiary.Rcl.Hybird.Layout
{
    public partial class UpdateDialog : UpdateDialogBase
    {
        private const string githubReleasesUrl = "https://github.com/zengxu7766-hub/PageNest/releases";
        private bool showUpdateMethodDialog;
        private List<DynamicListItem<UpdateMethod>> items = [];

        [Inject]
        private IPlatformIntegration PlatformIntegration { get; set; } = default!;
        [Inject]
        private IAccessExternal AccessExternal { get; set; } = default!;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            LoadView();
        }

        private void LoadView()
        {
            items = [];
            if (PlatformIntegration.CurrentPlatform.IsInAppStore())
            {
                items.Add(new(this, "App store", "store", ToUpdate, UpdateMethod.AppStore));
            }

            items.Add(new(this, "Github releases", "mdi:mdi-github", ToUpdate, UpdateMethod.GithubReleases));
        }

        public async Task ToUpdate(UpdateMethod updateMethod)
        {
            switch (updateMethod)
            {
                case UpdateMethod.AskEveryTime:
                    break;
                case UpdateMethod.AppStore:
                    await ToAppStore();
                    break;
                case UpdateMethod.GithubReleases:
                    await ToGithubReleases();
                    break;
                default:
                    break;
            }
        }

        private async Task ToAppStore()
        {
            showUpdateMethodDialog = false;
            bool flag = await AccessExternal.OpenAppStoreAppDetails();
            if (!flag)
            {
                await AlertService.ErrorAsync(I18n.T("Failed to open the application store"));
            }
        }

        private async Task ToGithubReleases()
        {
            showUpdateMethodDialog = false;
            await PlatformIntegration.OpenBrowser(githubReleasesUrl);
        }

        protected override Task ToUpdate()
        {
            showUpdateMethodDialog = true;
            return Task.CompletedTask;
        }
    }
}