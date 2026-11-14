using System.Text.Encodings.Web;
using System.Text.Json;

namespace SwashbucklerDiary.Rcl.Hybird.Services
{
    /// <summary>
    /// Shared JSON options. The default System.Text.Json encoder escapes every
    /// non-ASCII character (\u4ECA\u5929), which would make Chinese diary content
    /// unreadable to MCP clients. UnsafeRelaxedJsonEscaping keeps UTF-8 text as-is.
    /// </summary>
    internal static class McpJson
    {
        internal static readonly JsonSerializerOptions Options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }
}
