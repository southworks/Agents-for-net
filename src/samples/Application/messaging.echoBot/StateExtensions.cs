using Microsoft.Agents.BotBuilder.Application.State;
using System.Diagnostics.Metrics;

namespace EchoBot
{
    public static class StateExtensions
    {
        public static int MessageCount(this TurnState state)
        {
            var value = state.GetValue("conversation.countKey");
            return value == null ? 0 : (int)value;
        }

        public static void MessageCount(this TurnState state, int value)
        {
            state.SetValue("conversation.countKey", value);
        }
    }
}
