using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.MCP.Server.Transports
{
    public static class SseEventTypes
    {
        public const string Endpoint = "endpoint";
        public const string Close = "close";
        public const string Message = "message";
    }
}
