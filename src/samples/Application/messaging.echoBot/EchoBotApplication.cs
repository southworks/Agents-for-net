using EchoBot.Model;
using Microsoft.Agents.BotBuilder.Application;
using Microsoft.Agents.BotBuilder.Application.State;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace EchoBot
{
    public class EchoBotApplication : Application<AppState>
    {
        public EchoBotApplication(ApplicationOptions<AppState> options) : base(options)
        {
            OnConversationUpdate("membersAdded", WelcomeMessageAsync);

            // Listen for user to say "/reset" and then delete conversation state
            OnMessage("/reset", DeleteStateHandlerAsync);

            // Listen for ANY message to be received. MUST BE AFTER ANY OTHER MESSAGE HANDLERS
            OnActivity(ActivityTypes.Message, MessageHandlerAsync);
        }

        /// <summary>
        /// Handles members added events.
        /// </summary>
        public static async Task WelcomeMessageAsync(ITurnContext turnContext, TurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles "/reset" message.
        /// </summary>
        public static async Task DeleteStateHandlerAsync(ITurnContext turnContext, AppState turnState, CancellationToken cancellationToken)
        {
            turnState.DeleteConversationState();
            await turnContext.SendActivityAsync("Ok I've deleted the current conversation state", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Handles messages except "/reset".
        /// </summary>
        public static async Task MessageHandlerAsync(ITurnContext turnContext, AppState turnState, CancellationToken cancellationToken)
        {
            int count = turnState.Conversation.MessageCount;

            // Increment count state.
            turnState.Conversation.MessageCount = ++count;

            await turnContext.SendActivityAsync($"[{count}] you said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        }
    }
}
