
using Microsoft.Agents.BotBuilder.State;

namespace EchoBot
{
    public static class StateExtensions
    {
        public static int MessageCount(this ConversationState state)
        {
            return state.GetValue<int>("countKey");
        }

        public static void MessageCount(this ConversationState state, int value)
        {
            state.SetValue("countKey", value);
        }
    }
}
