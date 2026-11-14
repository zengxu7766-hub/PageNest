using System.Collections.Generic;

namespace SwashbucklerDiary.Rcl.Hybird.Services
{
    /// <summary>
    /// State shared by all <see cref="McpController"/> instances (EmbedIO creates
    /// a fresh controller for every request, so sessions cannot live on the controller).
    /// </summary>
    internal sealed class McpContext
    {
        private readonly object _sync = new();
        private readonly HashSet<string> _sessions = new();

        public string Token { get; init; } = string.Empty;

        public McpTools Tools { get; init; } = default!;

        public void AddSession(string id)
        {
            lock (_sync)
            {
                _sessions.Add(id);
            }
        }

        public bool HasSession(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            lock (_sync)
            {
                return _sessions.Contains(id);
            }
        }

        public void RemoveSession(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            lock (_sync)
            {
                _sessions.Remove(id);
            }
        }
    }
}
