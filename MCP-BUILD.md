# 侠客日记 · MCP Server 改造说明

本仓库：https://github.com/zengxu7766-hub/SwashbucklerDiary-MCP

基于 [Yu-Core/SwashbucklerDiary](https://github.com/Yu-Core/SwashbucklerDiary)
（上游 commit `f45df93`，AGPL-3.0）增加了 **MCP（Model Context Protocol）服务端接口**，
让 AI 客户端可以读写日记。

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
| `Rcl/wwwroot/i18n/*.json`（12 语言） | 新增 10 个 MCP 相关文案键 |
| `Rcl/Extensions/ServiceCollectionExtensions/AddRclDependencyInjection.cs` | `TryAddSingleton<IMcpService, NullMcpService>()` |
| `Maui/Extensions/ServiceCollectionExtensions/AddDependencyInjection.cs` | 在 `AddRclDependencyInjection()` **之前**注册真实 `McpService`（后注册者胜出） |
| `Maui/MauiProgram.cs` | `builder.Build()` 后调用 `IMcpService.Apply()` 启动服务 |
| `.github/workflows/build-android-apk.yml` | 免 secret 的一键出 APK 工作流 |
| `MCP-BUILD.md` | 本文件 |

**重要：隐私日记功能一行未删、未改。** 采用「隔离」而非「删除」策略：

- 隐私日记存在独立的第二个 SQLite 库，`BaseRepository` 会在 `TempSetting.PrivacyMode == true` 时切换到它。
- `McpTools.RunAsync()` 开头统一 `EnsureSafe()`：一旦处于隐私模式，所有工具直接拒绝服务。
- 列表查询基于 `QueryDiariesAsync()`（已排除模板），再在内存过滤掉遗留的 `Private` 行；`get/update/delete` 还会拒绝 `Template` 与 `Private`。
- 不调用任何 `MovePrivacy*`，不修改 `TempSetting.PrivacyMode`。
- 删除隐私功能会牵扯双数据库、导出/备份、WebDAV、12 语言文件等约 60 个文件，风险过高，故保留。

---

## 2. 编译 / 出 APK

### 本机已验证

- 装了纯 .NET 10 SDK（arm64），`dotnet build src/SwashbucklerDiary.Rcl.Hybird/SwashbucklerDiary.Rcl.Hybird.csproj`：
  **Build succeeded, 0 Error(s)**（仅既有第三方包警告）。
- 写了端到端测试（真实 EmbedIO 服务器 + 内存桩），**30 / 30 全部通过**：初始化、会话校验、7 个工具、隐私隔离、鉴权、错误处理、中文 UTF-8 输出、LAN 绑定等。

> 本机是 arm64 Android 手机（proot）。Android SDK 的 `aapt2` 只有 x86-64 原生二进制，
> 在此环境需要 qemu 包装且无法注册 binfmt，故 **不在本机出 APK**。

### 用 GitHub Actions 出 APK（推荐）

本仓库已托管在 [zengxu7766-hub/SwashbucklerDiary-MCP](https://github.com/zengxu7766-hub/SwashbucklerDiary-MCP)（公开，AGPL-3.0）。

1. 打开仓库 → **Actions** → 左侧 **Build Android APK (MCP fork)** → **Run workflow**。
2. 选 `arm64`（真机）或 `x64`（模拟器），是否附带到 Release；跑完在 **Artifacts** 下载 `SwashbucklerDiary-MCP-<arch>.apk`。

> **已实测（2026-11-15）**：首次 `arm64` 手动构建全流程成功（约 10 分钟），
> 产出 `android-arm64-apk` artifact ≈ 86 MB，内含 `arm64-v8a` 原生库与 debug 签名
> （`META-INF/ANDROIDD.RSA`）。本机也已把 APK 下载到手机存储供真机安装。

工作流要点：

- runner 用 `windows-latest`（x86-64），aapt2 原生可跑，避开本机 arm64 的死结。
- `dotnet workload install maui` + `setup-java 17`，与官方 `releases.yml` 一致。
- **不需要配置任何 secrets**：用 `keytool` 在运行时生成一次性 debug keystore 并签名。
- 产物是 debug 签名的 APK，可真机安装；不要用于应用商店正式发布。

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

### 已知限制

- 服务随 App 进程存活；安卓后台被系统回收后会失效，重开 App 即可（未做常驻前台 Service）。
- 不提供标签/资源的写入工具（避免多对多导航插入产生重复数据）。
- 处于隐私模式时 MCP 整体不可用（这是刻意的安全设计）。
- `resources/list`、`prompts/list` 返回空列表；暂不支持 SSE 长连接流（用 Streamable HTTP POST 即可）。
