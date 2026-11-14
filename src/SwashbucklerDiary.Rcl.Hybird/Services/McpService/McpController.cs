using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System.Text;
using System.Text.Json.Nodes;

namespace SwashbucklerDiary.Rcl.Hybird.Services
{
    /// <summary>
    /// MCP Streamable-HTTP endpoint at POST /mcp (JSON-RPC 2.0).
    /// A fresh controller instance is created per request by EmbedIO, so all
    /// state is held in <see cref="McpContext"/>.
    /// </summary>
    internal sealed class McpController : WebApiController
    {
        private const string ProtocolVersion = "2024-11-05";
        private const string SessionMissing = "缺少 Mcp-Session-Id，请先 POST initialize / Missing or invalid Mcp-Session-Id. POST initialize first.";

        private readonly McpContext _context;

        public McpController(McpContext context)
        {
            _context = context;
        }

        [Route(HttpVerbs.Post, "/mcp")]
        public async Task Post()
        {
            ApplyCors();

            // Optional bearer-token auth (McpAuthToken setting).
            if (!IsAuthorized())
            {
                await WriteJsonAsync(401, Error(null, -32001, "Unauthorized: invalid or missing bearer token."));
                return;
            }

            string body;
            using (var reader = new StreamReader(Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                await WriteJsonAsync(400, Error(null, -32700, "Parse error: empty body."));
                return;
            }

            // JSON-RPC batch is intentionally unsupported (simpler, safer).
            if (body.TrimStart().StartsWith('['))
            {
                await WriteJsonAsync(200, Error(null, -32600, "Invalid request: batch is not supported."));
                return;
            }

            JsonObject? root;
            try
            {
                root = JsonNode.Parse(body) as JsonObject;
            }
            catch
            {
                await WriteJsonAsync(200, Error(null, -32700, "Parse error: invalid JSON."));
                return;
            }
            if (root is null)
            {
                await WriteJsonAsync(200, Error(null, -32600, "Invalid request."));
                return;
            }

            var id = root["id"]?.DeepClone();
            string? method;
            try { method = root["method"]?.GetValue<string>(); }
            catch { method = null; }

            // Notifications have no id and must not produce a response body.
            bool isNotification = id is null;

            if (string.IsNullOrEmpty(method))
            {
                if (isNotification)
                {
                    await WriteEmptyAsync(202);
                    return;
                }
                await WriteJsonAsync(200, Error(id, -32600, "Invalid request: missing method."));
                return;
            }

            var @params = root["params"] as JsonObject;

            switch (method)
            {
                case "initialize":
                    {
                        var sessionId = Guid.NewGuid().ToString("N");
                        _context.AddSession(sessionId);
                        Response.Headers["Mcp-Session-Id"] = sessionId;

                        string? requested = null;
                        try { requested = @params?["protocolVersion"]?.GetValue<string>(); }
                        catch { }

                        var result = new JsonObject
                        {
                            ["protocolVersion"] = string.IsNullOrEmpty(requested) ? ProtocolVersion : requested,
                            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
                            ["serverInfo"] = new JsonObject
                            {
                                ["name"] = "SwashbucklerDiary",
                                ["version"] = "1.0.0"
                            }
                        };
                        await WriteJsonAsync(200, Ok(id, result));
                        return;
                    }

                case "notifications/initialized":
                case "notifications/cancelled":
                    await WriteEmptyAsync(202);
                    return;

                case "ping":
                    await WriteJsonAsync(200, Ok(id, new JsonObject()));
                    return;

                case "tools/list":
                    if (!HasSession())
                    {
                        await WriteJsonAsync(200, Error(id, -32000, SessionMissing));
                        return;
                    }
                    await WriteJsonAsync(200, Ok(id, new JsonObject { ["tools"] = _context.Tools.GetToolList() }));
                    return;

                case "tools/call":
                    if (!HasSession())
                    {
                        await WriteJsonAsync(200, Error(id, -32000, SessionMissing));
                        return;
                    }
                    {
                        string? name;
                        try { name = @params?["name"]?.GetValue<string>(); }
                        catch { name = null; }
                        if (string.IsNullOrEmpty(name))
                        {
                            await WriteJsonAsync(200, Error(id, -32602, "Invalid params: tool name is required."));
                            return;
                        }
                        var args = @params?["arguments"] as JsonObject;
                        var (text, isError) = await _context.Tools.CallAsync(name, args);
                        var result = new JsonObject
                        {
                            ["content"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "text", ["text"] = text }
                            },
                            ["isError"] = isError
                        };
                        await WriteJsonAsync(200, Ok(id, result));
                        return;
                    }

                case "resources/list":
                    if (!HasSession())
                    {
                        await WriteJsonAsync(200, Error(id, -32000, SessionMissing));
                        return;
                    }
                    await WriteJsonAsync(200, Ok(id, new JsonObject { ["resources"] = new JsonArray() }));
                    return;

                case "prompts/list":
                    if (!HasSession())
                    {
                        await WriteJsonAsync(200, Error(id, -32000, SessionMissing));
                        return;
                    }
                    await WriteJsonAsync(200, Ok(id, new JsonObject { ["prompts"] = new JsonArray() }));
                    return;

                default:
                    if (isNotification)
                    {
                        await WriteEmptyAsync(202);
                        return;
                    }
                    await WriteJsonAsync(200, Error(id, -32601, $"Method not found: {method}"));
                    return;
            }
        }

        [Route(HttpVerbs.Get, "/mcp")]
        public Task Get()
        {
            ApplyCors();
            // Streamable HTTP: no legacy standalone SSE stream; clients use POST.
            Response.StatusCode = 405;
            Response.Headers["Allow"] = "POST, DELETE, OPTIONS";
            return Task.CompletedTask;
        }

        [Route(HttpVerbs.Options, "/mcp")]
        public Task Options()
        {
            ApplyCors();
            Response.StatusCode = 204;
            return Task.CompletedTask;
        }

        [Route(HttpVerbs.Delete, "/mcp")]
        public async Task Delete()
        {
            ApplyCors();
            if (!IsAuthorized())
            {
                await WriteJsonAsync(401, Error(null, -32001, "Unauthorized: invalid or missing bearer token."));
                return;
            }
            _context.RemoveSession(Request.Headers["Mcp-Session-Id"]);
            Response.StatusCode = 204;
        }

        /// <summary>Whether a valid session was established via initialize.</summary>
        private bool HasSession()
            => _context.HasSession(Request.Headers["Mcp-Session-Id"]);

        private bool IsAuthorized()
        {
            if (string.IsNullOrEmpty(_context.Token))
            {
                return true;
            }
            var header = Request.Headers["Authorization"];
            return !string.IsNullOrEmpty(header)
                   && header.Equals($"Bearer {_context.Token}", StringComparison.Ordinal);
        }

        private void ApplyCors()
        {
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, DELETE, OPTIONS";
            Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, Mcp-Session-Id";
            Response.Headers["Access-Control-Expose-Headers"] = "Mcp-Session-Id";
        }

        private async Task WriteJsonAsync(int status, JsonNode payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload.ToJsonString(McpJson.Options));
            Response.StatusCode = status;
            Response.ContentType = "application/json; charset=utf-8";
            Response.ContentLength64 = bytes.Length;
            await Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        private async Task WriteEmptyAsync(int status)
        {
            Response.StatusCode = status;
            Response.ContentLength64 = 0;
            await Task.CompletedTask;
        }

        private static JsonObject Ok(JsonNode? id, JsonObject result)
            => new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = result
            };

        private static JsonObject Error(JsonNode? id, int code, string message)
            => new()
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["error"] = new JsonObject { ["code"] = code, ["message"] = message }
            };
    }
}
