# 栖页 / PageNest · MCP Server 改造说明

本仓库：https://github.com/zengxu7766-hub/PageNest

基于 [Yu-Core/SwashbucklerDiary](https://github.com/Yu-Core/SwashbucklerDiary)
（上游 commit `f45df93`，AGPL-3.0）增加了 **MCP（Model Context Protocol）服务端接口**，
让 AI 客户端可以读写日记；并做了本地化定制：**应用改名「栖页」/ 英文名 PageNest、更换图标、
界面语言精简为简体中文/繁体中文/English 三种、精简关于/我的页、检查更新指向本仓库、
APK 使用固定签名（可直接覆盖升级）**。

> 本项目仍采用 **AGPL-3.0** 协议，`LICENSE.txt` 必须保留；若对外分发或公开仓库，
> 请同样以 AGPL-3.0 开源，并在 README 注明本改动来源。

---

## 1. 改了什么

| 位置 | 内容 |
| --- | --- |
| `src/SwashbucklerDiary.Rcl/Services/Mcp/` | `IMcpService` 接口 + `NullMcpService`（默认空实现，给不引用 Rcl.Hybird 的宿主用） |
| `src/SwashbucklerDiary.Rcl.Hybird/Services/McpService/` | MCP 服务端：`McpService`（生命周期/EmbedIO 宿主）、`McpController`（HTTP/JSON-RPC）、`McpContext`（会话）、`McpTools`（工具集）、`McpJson`（UTF-8 编码器） |
| `Rcl/Services/SettingService/Setting.cs` | 新增 4 个设置项：`McpEnabled`(默认关闭)、`McpPort`(0=自动)、`McpAllowLan`(默认仅本机)、`McpAuthToken`(默认空) |
| `Rcl/Pages/Mine/Settings/SettingPage.razor(.cs)` | 设置页「Other」分组新增 MCP Server 卡片：开关、状态地址、端口/令牌弹窗 |
| `Rcl/wwwroot/i18n/*.json`（3 语言） | 新增 10 个 MCP 相关文案键 |
| `Rcl/Extensions/ServiceCollectionExtensions/AddRclDependencyInjection.cs` | `TryAddSingleton<IMcpService, NullMcpService>()` |
| `Maui/Extensions/ServiceCollectionExtensions/AddDependencyInjection.cs` | 在 `AddRclDependencyInjection()` **之前**注册真实 `McpService`（后注册者胜出） |
| `Maui/MauiProgram.cs` | `builder.Build()` 后调用 `IMcpService.Apply()` 启动服务 |
| `.github/workflows/build-android-apk.yml` | 一键出 APK 工作流（从 Actions Secret 恢复固定签名） |
| `MCP-BUILD.md` | 本文件 |

### 1.1 本地化定制（2026-11-15）

| 位置 | 内容 |
| --- | --- |
| `Maui/SwashbucklerDiary.Maui.csproj` | `<ApplicationTitle>栖页</ApplicationTitle>`；4 个 `<MauiIcon>` 改为单图 `appicon.png` + `BaseSize="500,500"`。**`ApplicationId` 保持 `com.yucore.swashbucklerdiary` 不变**，以便覆盖安装、不丢日记数据 |
| `Maui/Resources/AppIcon/appicon.png`(+`.svg`)、`Resources/Splash/splash.jpg`、`Rcl/wwwroot/logo/logo.jpg` | 换成用户提供的「淡青圆角方块 + 打开的书 + 太阳」图标（500×500 RGBA，四角透明） |
| `Maui/Platforms/Android/Resources/values*/Strings.xml`（13→4 个） | 桌面应用名：中文环境「栖页」、英文/默认 `PageNest`（详见 1.2） |
| `Rcl/wwwroot/i18n/*.json`（12→3 语言） | 键 `"Swashbuckler Diary"` 的值：中文「栖页」、英文 `PageNest`（键名不变，避免改 7 处 `I18n.T(...)` 源码） |
| `Rcl/Pages/Mine/About/AboutPage.razor(.cs)` | 去掉「APP备案号:辽ICP备2023009191号-2A」整块、去掉「捐助我们」与「给个好评」入口及相关字段/方法/注入 |
| `Rcl/Pages/Mine/MinePage.razor(.cs)` | 去掉「联系我们」及其弹窗、反馈数据、邮件/Github/QQ 群方法、`IAccessExternal`/`ILogger` 注入 |
| `Rcl/Services/VersionUpdataManager/VersionUpdataManager.cs` | 检查更新 API 改为 `https://api.github.com/repos/zengxu7766-hub/PageNest/releases/latest`（去掉 Gitee 分流），并用 `Version.TryParse` 兼容本仓库 `apk-N` 这类非版本 tag |
| `Rcl.Hybird/Layout/UpdateDialog.razor(.cs)`、`Rcl.Web/Layout/UpdateDialog.cs` | 更新地址改到本仓库 Releases；删掉上游作者的「移动云盘」下载项及弹窗 |
| `Rcl/wwwroot/json/{app-features,code-source,feedback-type}/*.json` | 搜索索引移除捐助/好评条目；「查看源码」指向本 fork（保留 Upstream 署名）；清理上游邮箱/QQ 群 |

### 1.2 英文名 PageNest 与语言精简（2026-11-15）

- **中文（简体/繁体）显示「栖页」，英文及默认显示 `PageNest`**：csproj `ApplicationTitle` 仍为「栖页」；
  Android `values/`、`values-en-rUS/` 的 `app_name` = `PageNest`，`values-zh-rCN/`、`values-zh-rTW/` = 「栖页」；
  `en-US.json` 的品牌值改 `PageNest`，`zh-CN/zh-TW.json` 改「栖页」；README、issue 模板、Server `App.razor`、docs md 同步。
- **界面语言只保留 3 种**：`supportedCultures.json` = `["zh-CN","zh-TW","en-US"]`；
  删掉 9 个 i18n json、9 个 Android `values-*` 目录、36 个非三种语言的 `wwwroot/docs/*.md`。
  `ReadI18nTextAsync` 在文件缺失时回退 `en-US`，故 `functional-description` 等仅有 en-US + zh-CN 的文档对繁体用户安全回退英文。
- **`ApplicationId` 始终不变**（`com.yucore.swashbucklerdiary`）：保证与旧版覆盖安装、不丢日记数据。

### 1.3 纯 Android-only 裁剪（2026-11-15）

本 fork 只出 Android APK，因此把 Maui 工程收敛为单目标、删掉所有非 Android 平台代码：

| 位置 | 内容 |
| --- | --- |
| `Maui/SwashbucklerDiary.Maui.csproj` | `<TargetFrameworks>net10.0-android</TargetFrameworks>` 单目标；删除 Windows 条件追加、ios/maccatalyst/windows 的版本与 `SupportedOSPlatformVersion` 条件、`MtouchInterpreter`、5 个按平台 `Compile Remove` 的 ItemGroup、3 个非 android `MauiIcon`、iOS/MacCatalyst 的 `BundleResource`、状态栏 css 的非 android 分支；`SupportedOSPlatformVersion` 固定 `26.0`、`UseMonoRuntime=false`（Release, CoreCLR）保留；`ApplicationId` 不变 |
| `Maui/Platforms/{iOS,MacCatalyst,Windows}/` | 整目录删除（共 48 个文件：AppDelegate/Program/Info.plist/Entitlements/plist、Windows App.xaml/Package.appxmanifest/app.manifest、12 语言 Resources 等）。`Platforms/Android/` 完整保留 |
| `Maui/MainPage.xaml.{MaciOS,Windows,iOS}.cs`、`BlazorWebView/MaciOS/`、`Essentials/**/*.MaciOS.cs`、`Essentials/AppLifecycle/AppActivation.Windows.cs` | 非 Android 平台分部实现删除；保留 `MainPage.xaml.Android.cs`（`BlazorWebViewInitializing`/`BlazorWebViewInitialized` 的 Android 实现）与全部 `*.Android.cs` |
| `Maui/MauiProgram.cs`、`App.xaml.cs`、`MainPage.xaml.cs`、`Essentials/AppFileSystem/AppFileSystem.cs` | 清理引用已删类型的死 `#if`（`WebViewNavigationDelegate`、`TitleBarButtonDirectionHelper`、`Initialize()`、`_platformAppDataDirectory/CacheDirectory`）；`AppFileSystem` 直接用 `FileSystem.Current.*`；**MCP 启动钩子 `IMcpService.Apply()` 保留** |
| 其余 `#if ANDROID/IOS/WINDOWS/...` 业务分支 | 保留不动：预编译指令在 Android 编译时自动排除，删除需重写工作逻辑、风险大收益小 |

`src/` 下另外 5 个非安卓项目（Gtk / Server / WebAssembly / Rcl.Web / Maui.Essentials）**保留在本地仓库**（交付的 zip 从来只包含 4 个安卓必需项目），避免大范围删除引入风险；`SwashbucklerDiary.Maui.slnx` 也仍只含 4 个项目。裁剪后经 GitHub Actions `build-android-apk.yml` 真出 APK 验证。

**重要：隐私日记功能一行未删、未改。** 采用「隔离」而非「删除」策略：

- 隐私日记存在独立的第二个 SQLite 库，`BaseRepository` 会在 `TempSetting.PrivacyMode == true` 时切换到它。
- `McpTools.RunAsync()` 开头统一 `EnsureSafe()`：一旦处于隐私模式，所有工具直接拒绝服务。
- 列表查询基于 `QueryDiariesAsync()`（已排除模板），再在内存过滤掉遗留的 `Private` 行；`get/update/delete` 还会拒绝 `Template` 与 `Private`。
- 不调用任何 `MovePrivacy*`，不修改 `TempSetting.PrivacyMode`。
- 删除隐私功能会牵扯双数据库、导出/备份、WebDAV、多语言文件等约 60 个文件，风险过高，故保留。

---

## 2. 编译 / 出 APK

### 本机已验证

- 装了纯 .NET 10 SDK（arm64），`dotnet build src/SwashbucklerDiary.Rcl.Hybird/SwashbucklerDiary.Rcl.Hybird.csproj`：
  **Build succeeded, 0 Error(s)**（仅既有第三方包警告）。
- 写了端到端测试（真实 EmbedIO 服务器 + 内存桩），**30 / 30 全部通过**：初始化、会话校验、7 个工具、隐私隔离、鉴权、错误处理、中文 UTF-8 输出、LAN 绑定等。

> 本机是 arm64 Android 手机（proot）。Android SDK 的 `aapt2` 只有 x86-64 原生二进制，
> 在此环境需要 qemu 包装且无法注册 binfmt，故 **不在本机出 APK**。

### 用 GitHub Actions 出 APK（推荐）

本仓库已托管在 [zengxu7766-hub/PageNest](https://github.com/zengxu7766-hub/PageNest)（公开，AGPL-3.0）。

1. 打开仓库 → **Actions** → 左侧 **Build Android APK (MCP fork)** → **Run workflow**。
2. 选 `arm64`（真机）或 `x64`（模拟器），是否附带到 Release；跑完在 **Artifacts** 下载 `PageNest-<arch>.apk`。

> **已实测（2026-11-15）**：`arm64` 手动构建全流程成功（约 10 分钟），
> 产出 `android-arm64-apk` artifact ≈ 86 MB，内含 `arm64-v8a` 原生库与签名
> （`META-INF/ANDROIDD.RSA`）。本机也已把 APK 下载到手机存储供真机安装。
>
> **固定签名已实测**：连续触发 run #3、run #4 两次构建，分别用
> `keytool -printcert -jarfile` 读取 APK 证书，两次 SHA256 完全一致
> （`86:65:…:CE:F2`，且等于持久 keystore 指纹），证明每次产物可互相覆盖升级。

工作流要点：

- runner 用 `windows-latest`（x86-64），aapt2 原生可跑，避开本机 arm64 的死结。
- `dotnet workload install maui` + `setup-java 17`，与官方 `releases.yml` 一致。
- **固定签名（2026-11-15 起）**：keystore 只生成一次，base64 后存入仓库 Actions Secret
  `ANDROID_KEYSTORE_BASE64`；每次构建由 `Restore the fixed signing keystore` 步骤解码复用，
  并在日志打印证书 `SHA256` 指纹。**每次出的 APK 签名一致，可以直接覆盖升级**，不必卸载、不丢日记数据。
  - 证书：`CN=QiYe, O=SwashbucklerDiary-MCP, C=CN`（组织名是 keystore 建立时写入的，不随仓库改名变化），有效期至 2054-04-02，
    SHA256 `86:65:32:61:23:8A:C6:A5:C5:12:B2:C5:D1:6D:C9:8A:37:0F:1A:56:A5:3D:D9:7C:BC:B6:31:99:8C:D0:CE:F2`。
  - keystore 备份在手机存储 `Download/DSH-Go/qiye-release.keystore`（storepass/keypass 均 `android`，alias `androiddebugkey`）。
    **请自行妥善保管**：Secret 只能写入、不能读回；丢了它就无法再产出同签名 APK。
  - ⚠️ 早期（2026-11-15 之前）的 APK 用的是「一次性 debug keystore」，与固定签名不同，
    **首次换到固定签名版仍需卸载旧版**（会清掉本地日记，先导出/备份）；之后即可一直覆盖升级。
- 产物是 debug 别名签名的 APK，可真机安装；不要用于应用商店正式发布。

本机命令备查：

```bash
dotnet build src/SwashbucklerDiary.Rcl.Hybird/SwashbucklerDiary.Rcl.Hybird.csproj -c Debug
```

---

## 3. 怎么用 MCP

1. App 里：**设置 → 其他 → MCP Server**，打开开关。
2. 卡片上会显示地址，例如 `http://127.0.0.1:52137/mcp`。
3. 默认只监听 `127.0.0.1`（仅本机 + `adb forward`）。开 **Allow LAN access** 才会监听 `0.0.0.0`，同一 Wi-Fi 下其它设备可访问（注意日记隐私）。
4. 可设 **Access token**：客户端需带 `Authorization: Bearer <token>`，否则 401。
5. 端口默认 `0`（自动选空闲端口），也可固定，方便 `adb forward`：

   ```bash
   adb forward tcp:52137 tcp:52137
   ```

MCP 客户端配置（Streamable HTTP）：

```json
{
  "mcpServers": {
    "diary": {
      "url": "http://127.0.0.1:52137/mcp",
      "headers": { "Authorization": "Bearer <你的 token>" }
    }
  }
}
```

协议流程：`POST initialize` → 从响应头拿 `Mcp-Session-Id` → `notifications/initialized` →
之后每个请求带 `Mcp-Session-Id` 头 → `tools/list` / `tools/call` → 结束可 `DELETE /mcp`。

### 提供的 7 个工具

| 工具 | 作用 |
| --- | --- |
| `list_diaries` | 按时间倒序列出日记（不含正文），支持 `limit` / `offset` |
| `search_diaries` | 按标题/正文搜索（大小写不敏感），返回含正文 |
| `get_diary` | 按 `id` 取单篇（含标签） |
| `create_diary` | 新建日记，返回新 `id`（`content` 必填） |
| `update_diary` | 按 `id` 改 `title/content/mood/weather/location/top` |
| `delete_diary` | 按 `id` 删除（连带清理标签关联） |
| `list_tags` | 列出标签及日记数 |

### 后台保活（Android 前台 Service）

开启 MCP 后，App 会启动一个**前台 Service**，在状态栏显示一条常驻通知（"栖页 MCP 正在运行"），
把 App 进程提升为前台优先级，避免系统后台回收导致 MCP 服务中断。关闭 MCP 时服务与通知一并停止。

实际效果与边界：

- **划掉通知**：不行。通知 `setOngoing(true)`，常规手势划不掉（这是前台服务的标准行为）。
- **划掉最近任务**：取决于 ROM。因为进程级优先级被提升，多数系统上服务仍存活；部分激进 ROM 可能会连带清理，需按下方白名单处理。
- **强行停止（设置 → 应用 → 强行停止）**：必死，Android 安全模型无解，只能重新打开 App 恢复。
- **国产 ROM（小米/华为/OPPO/vivo 等）**：前台服务本身也不保证 100% 存活。请把栖页加入「自启动 / 后台运行 / 电池优化白名单」，并允许后台通知，否则长时间锁屏后仍可能被冻结。

### 已知限制

- 服务随 App 进程存活；前台 Service 大幅提升存活率，但强行停止或极激进的 ROM 策略下仍可能失效，重开 App 即可恢复。
- 不提供标签/资源的写入工具（避免多对多导航插入产生重复数据）。
- 处于隐私模式时 MCP 整体不可用（这是刻意的安全设计）。
- `resources/list`、`prompts/list` 返回空列表；暂不支持 SSE 长连接流（用 Streamable HTTP POST 即可）。
