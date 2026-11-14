using SwashbucklerDiary.Rcl.Services;
using SwashbucklerDiary.Shared;
using System.Text.Json.Nodes;

namespace SwashbucklerDiary.Rcl.Hybird.Services
{
    /// <summary>
    /// Implements the diary / tag tools exposed over MCP.
    /// IMPORTANT (privacy): every tool first calls <see cref="EnsureSafe"/>. The
    /// app stores "private mode" diaries in a SEPARATE SQLite database and the
    /// repositories switch to it whenever TempSetting.PrivacyMode is true (see
    /// BaseRepository.ConfigId). We never touch that switch, we refuse to serve
    /// anything while that mode is active, and we additionally filter out legacy
    /// DiaryModel.Private rows and templates. No privacy code is modified.
    /// </summary>
    internal sealed class McpTools
    {
        private readonly IDiaryService _diaryService;
        private readonly ITagService _tagService;
        private readonly ISettingService _settingService;

        public McpTools(IDiaryService diaryService, ITagService tagService, ISettingService settingService)
        {
            _diaryService = diaryService;
            _tagService = tagService;
            _settingService = settingService;
        }

        private const string ToolsJson = """
        [
          {
            "name": "list_diaries",
            "description": "List diary entries (newest first). Private-mode and template entries are never returned.",
            "inputSchema": {
              "type": "object",
              "properties": {
                "limit": { "type": "integer", "description": "Max number of entries to return (default 20, max 100)." },
                "offset": { "type": "integer", "description": "Number of entries to skip (default 0)." }
              },
              "required": []
            }
          },
          {
            "name": "search_diaries",
            "description": "Search diary entries whose title or content contains the given text (case-insensitive). Private/template excluded.",
            "inputSchema": {
              "type": "object",
              "properties": {
                "query": { "type": "string", "description": "Text to search for." },
                "limit": { "type": "integer", "description": "Max results (default 20, max 100)." }
              },
              "required": ["query"]
            }
          },
          {
            "name": "get_diary",
            "description": "Get one diary entry by id, including its tags.",
            "inputSchema": {
              "type": "object",
              "properties": {
                "id": { "type": "string", "description": "The diary GUID." }
              },
              "required": ["id"]
            }
          },
          {
            "name": "create_diary",
            "description": "Create a new diary entry. Returns the new id.",
            "inputSchema": {
              "type": "object",
              "properties": {
                "title": { "type": "string", "description": "Diary title." },
                "content": { "type": "string", "description": "Diary body (Markdown supported)." },
                "mood": { "type": "string", "description": "Mood." },
                "weather": { "type": "string", "description": "Weather." },
                "location": { "type": "string", "description": "Location." },
                "top": { "type": "boolean", "description": "Pin to top." }
              },
              "required": ["content"]
            }
          },
          {
            "name": "update_diary",
            "description": "Update scalar fields of an existing diary entry. Only provided fields are changed.",
            "inputSchema": {
              "type": "object",
              "properties": {
                "id": { "type": "string", "description": "The diary GUID." },
                "title": { "type": "string" },
                "content": { "type": "string" },
                "mood": { "type": "string" },
                "weather": { "type": "string" },
                "location": { "type": "string" },
                "top": { "type": "boolean" }
              },
              "required": ["id"]
            }
          },
          {
            "name": "delete_diary",
            "description": "Delete a diary entry by id (cleans up tag/resource links).",
            "inputSchema": {
              "type": "object",
              "properties": {
                "id": { "type": "string", "description": "The diary GUID." }
              },
              "required": ["id"]
            }
          },
          {
            "name": "list_tags",
            "description": "List all tags with their diary counts.",
            "inputSchema": { "type": "object", "properties": {}, "required": [] }
          }
        ]
        """;

        public JsonArray GetToolList()
        {
            // Parse once per call; avoids reflection-based serialization (trimming/AOT safe).
            return JsonNode.Parse(ToolsJson)!.AsArray();
        }

        public async Task<(string Text, bool IsError)> CallAsync(string name, JsonObject? args)
        {
            try
            {
                var a = args ?? new JsonObject();
                var text = await RunAsync(name, a);
                return (text, false);
            }
            catch (Exception ex)
            {
                var err = new JsonObject { ["error"] = ex.Message };
                return (err.ToJsonString(McpJson.Options), true);
            }
        }

        private async Task<string> RunAsync(string name, JsonObject a)
        {
            // Single privacy gate for ALL tools.
            EnsureSafe();

            switch (name)
            {
                case "list_diaries": return await ListDiariesAsync(a);
                case "search_diaries": return await SearchDiariesAsync(a);
                case "get_diary": return await GetDiaryAsync(a);
                case "create_diary": return await CreateDiaryAsync(a);
                case "update_diary": return await UpdateDiaryAsync(a);
                case "delete_diary": return await DeleteDiaryAsync(a);
                case "list_tags": return await ListTagsAsync(a);
                default: throw new InvalidOperationException($"Unknown tool: {name}");
            }
        }

        private void EnsureSafe()
        {
            // Refuse to operate whenever the user is inside private mode: the
            // repositories would resolve to the private database in that state.
            if (_settingService.GetTemp(s => s.PrivacyMode))
            {
                throw new InvalidOperationException("MCP 接口在隐私模式下不可用，请先退出隐私模式。 / MCP is unavailable in privacy mode. Exit private mode first.");
            }
        }

        /// <summary>Load all visible (non-private, non-template) diaries.</summary>
        private async Task<List<DiaryModel>> LoadVisibleAsync()
        {
            var diaries = await _diaryService.QueryDiariesAsync();
#pragma warning disable CS0618 // DiaryModel.Private is obsolete but still present on legacy rows
            return diaries.Where(d => !d.Private).ToList();
#pragma warning restore CS0618
        }

        private async Task<string> ListDiariesAsync(JsonObject a)
        {
            int limit = Clamp(Int(a, "limit", 20), 1, 100);
            int offset = Math.Max(0, Int(a, "offset", 0));
            var all = (await LoadVisibleAsync())
                .OrderByDescending(d => d.CreateTime)
                .Skip(offset)
                .Take(limit)
                .ToList();

            var arr = new JsonArray();
            foreach (var d in all)
            {
                arr.Add(DiaryToJson(d, includeContent: false));
            }

            var result = new JsonObject
            {
                ["count"] = arr.Count,
                ["offset"] = offset,
                ["diaries"] = arr
            };
            return result.ToJsonString(McpJson.Options);
        }

        private async Task<string> SearchDiariesAsync(JsonObject a)
        {
            var query = Str(a, "query");
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("query is required.");
            }
            int limit = Clamp(Int(a, "limit", 20), 1, 100);
            var q = query.Trim();

            var matches = (await LoadVisibleAsync())
                .Where(d => Contains(d.Title, q) || Contains(d.Content, q))
                .OrderByDescending(d => d.CreateTime)
                .Take(limit)
                .ToList();

            var arr = new JsonArray();
            foreach (var d in matches)
            {
                arr.Add(DiaryToJson(d, includeContent: true));
            }

            var result = new JsonObject
            {
                ["query"] = q,
                ["count"] = arr.Count,
                ["diaries"] = arr
            };
            return result.ToJsonString(McpJson.Options);
        }

        private async Task<string> GetDiaryAsync(JsonObject a)
        {
            var diary = await RequireDiaryAsync(a);
            return DiaryToJson(diary, includeContent: true).ToJsonString(McpJson.Options);
        }

        private async Task<string> CreateDiaryAsync(JsonObject a)
        {
            var content = Str(a, "content");
            if (content is null)
            {
                throw new ArgumentException("content is required.");
            }

            var diary = new DiaryModel
            {
                Id = Guid.NewGuid(),
                Title = Str(a, "title"),
                Content = content,
                Mood = Str(a, "mood"),
                Weather = Str(a, "weather"),
                Location = Str(a, "location"),
                Top = Bool(a, "top") ?? false,
                Template = false,
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };

            var ok = await _diaryService.AddAsync(diary);
            var result = new JsonObject
            {
                ["success"] = ok,
                ["id"] = diary.Id.ToString()
            };
            return result.ToJsonString(McpJson.Options);
        }

        private async Task<string> UpdateDiaryAsync(JsonObject a)
        {
            var diary = await RequireDiaryAsync(a);

            if (a.ContainsKey("title")) diary.Title = Str(a, "title");
            if (a.ContainsKey("content")) diary.Content = Str(a, "content");
            if (a.ContainsKey("mood")) diary.Mood = Str(a, "mood");
            if (a.ContainsKey("weather")) diary.Weather = Str(a, "weather");
            if (a.ContainsKey("location")) diary.Location = Str(a, "location");
            if (a.ContainsKey("top")) diary.Top = Bool(a, "top") ?? diary.Top;
            diary.UpdateTime = DateTime.Now;

            var ok = await _diaryService.UpdateAsync(diary, it => new
            {
                it.Title, it.Content, it.Mood, it.Weather, it.Location, it.Top, it.UpdateTime
            });
            var result = new JsonObject
            {
                ["success"] = ok,
                ["id"] = diary.Id.ToString()
            };
            return result.ToJsonString(McpJson.Options);
        }

        private async Task<string> DeleteDiaryAsync(JsonObject a)
        {
            var diary = await RequireDiaryAsync(a);
            // Use the entity overload so DiaryRepository.DeleteNav cleans up links.
            var ok = await _diaryService.DeleteAsync(diary);
            var result = new JsonObject
            {
                ["success"] = ok,
                ["id"] = diary.Id.ToString()
            };
            return result.ToJsonString(McpJson.Options);
        }

        private async Task<string> ListTagsAsync(JsonObject a)
        {
            var tags = await _tagService.QueryAsync();
            Dictionary<Guid, int> counts = new();
            try
            {
                counts = await _tagService.TagsDiaryCount();
            }
            catch
            {
                // counts are best-effort only
            }

            var arr = new JsonArray();
            foreach (var t in tags.OrderByDescending(t => t.CreateTime))
            {
                counts.TryGetValue(t.Id, out var c);
                arr.Add(new JsonObject
                {
                    ["id"] = t.Id.ToString(),
                    ["name"] = t.Name,
                    ["count"] = c
                });
            }
            var result = new JsonObject
            {
                ["count"] = arr.Count,
                ["tags"] = arr
            };
            return result.ToJsonString(McpJson.Options);
        }

        /// <summary>Find a diary by id and reject templates / legacy private rows.</summary>
        private async Task<DiaryModel> RequireDiaryAsync(JsonObject a)
        {
            var idStr = Str(a, "id");
            if (!Guid.TryParse(idStr, out var id))
            {
                throw new ArgumentException("A valid 'id' (GUID) is required.");
            }
            var diary = await _diaryService.FindAsync(id);
            if (diary is null)
            {
                throw new InvalidOperationException("Diary not found.");
            }
            if (diary.Template)
            {
                throw new InvalidOperationException("Templates are not accessible via MCP.");
            }
#pragma warning disable CS0618
            if (diary.Private)
            {
                throw new InvalidOperationException("This entry is private and not accessible via MCP.");
            }
#pragma warning restore CS0618
            return diary;
        }

        private static JsonObject DiaryToJson(DiaryModel d, bool includeContent)
        {
            var obj = new JsonObject
            {
                ["id"] = d.Id.ToString(),
                ["title"] = d.Title,
                ["mood"] = d.Mood,
                ["weather"] = d.Weather,
                ["location"] = d.Location,
                ["top"] = d.Top,
                ["createTime"] = d.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                ["updateTime"] = d.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
            if (includeContent)
            {
                obj["content"] = d.Content;
            }

            var tags = new JsonArray();
            if (d.Tags is not null)
            {
                foreach (var t in d.Tags)
                {
                    tags.Add(new JsonObject
                    {
                        ["id"] = t.Id.ToString(),
                        ["name"] = t.Name
                    });
                }
            }
            obj["tags"] = tags;
            return obj;
        }

        // ---- tolerant argument readers (MCP clients send loosely-typed JSON) ----

        private static string? Str(JsonObject a, string key)
        {
            try { return a[key]?.GetValue<string>(); }
            catch { return null; }
        }

        private static bool? Bool(JsonObject a, string key)
        {
            try { return a[key]?.GetValue<bool>(); }
            catch { return null; }
        }

        private static int Int(JsonObject a, string key, int fallback)
        {
            try
            {
                var node = a[key];
                if (node is null) return fallback;
                return node.GetValue<int>();
            }
            catch { return fallback; }
        }

        private static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));

        private static bool Contains(string? haystack, string needle)
            => !string.IsNullOrEmpty(haystack)
               && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
