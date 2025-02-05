using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.BotBuilder.App;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace EchoBot
{
    public class EchoBotApplication : Application
    {
        public EchoBotApplication(ApplicationOptions options) : base(options)
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
        public static async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
        public static async Task DeleteStateHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnState.Conversation.DeleteStateAsync(turnContext, cancellationToken);
            await turnContext.SendActivityAsync("Ok I've deleted the current conversation state", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Handles messages except "/reset".
        /// </summary>
        public static async Task MessageHandlerAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            int count = turnState.Conversation.MessageCount();

            // Increment count state.
            turnState.Conversation.MessageCount(++count);

            await turnContext.SendActivityAsync($"[{count}] you said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        }
    }
}
