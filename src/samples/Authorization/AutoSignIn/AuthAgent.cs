// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AuthorizationAgent;

public class AuthAgent : AgentApplication
{
    /// <summary>
    /// Describes the agent registration for the Authorization Agent
    /// This agent will handle the sign-in and sign-out processes for a user.
    /// </summary>
    /// <param name="options">AgentApplication Configuration objects to configure and setup the Agent Application</param>
    public AuthAgent(AgentApplicationOptions options) : base(options)
    {
        /*
         During setup of the Agent Application, Register Event Handlers for the Agent. 
         For this example we will register a welcome message for the user when they join the conversation, then configure sign-in and sign-out commands.
         Additionally, we will add events to handle notifications of sign-in success and failure,  these notifications will report the local log instead of back to the calling agent. .

         This handler should only register events and setup state as it can be called multiple times before agent events are invoked. 
        */



        /*
         When a conversation update event is triggered. 
        */
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);

        /*
         Handles the user sending a Login or LogOut command using the specific keywords '-signin' and '-signout'
        */
        OnMessage("-signin", SignInAsync);
        OnMessage("-signout", SignOutAsync);


        /*
         The Authorization Class provides methods and properties to manage and access user authentication tokens
         You can use this class to interact with the authentication process, including signing in and signing out users, accessing tokens, and handling authentication events.
         */

        // Register Events for SignIn and SignOut on the authentication class to track the status of the user, notify the user of the status of their authentication process, or log the status of the authentication process.
        Authorization.OnUserSignInSuccess(OnUserSignInSuccess);
        Authorization.OnUserSignInFailure(OnUserSignInFailure);

        // Registers a general event handler that will pick up any message activity that is not covered by the previous events handlers. 
        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    /// <summary>
    /// This method is called to handle the conversation update event when a new member is added to or removed from the conversation.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        /*
        In this example, we will send a welcome message to the user when they join the conversation.
        We do this by iterating over the incoming activity members added to the conversation and checking if the member is not the agent itself.
        Then a greeting notice is provided to each new member of the conversation.
        */
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Welcome to AuthorizationAgent. Type 'auto' to demonstrate Auto SignIn. Type '/signin' to sign in for graph.  Type '/signout' to sign-out.  Anything else will be repeated back."), cancellationToken);
            }
        }
    }

    /// <summary>
    /// This method is called by the `OnMessage("-signin", SignInAsync);` registration to handle the sign-in process for the user. 
    /// This is a generic message handler pattern used by the agent SDK to allow a developer to attach specific implementation to a specific message.
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task SignInAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        /*
         We are using the Authorization class to sign in the user to the authorization provider.
         */
        await Authorization.SignInUserAsync(turnContext, turnState, "graph", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// This method is called by the `OnMessage("-signout", SignOutAsync);` registration to handle the sign-out process for the user.
    /// This is a generic message handler pattern used by the agent SDK to allow a developer to attach specific implementation to a specific message./// 
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    private async Task SignOutAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        /*
         Here we are using the Authorization class to sign out the user from the authorization provider.
         */
        await Authorization.SignOutUserAsync(turnContext, turnState, cancellationToken: cancellationToken);

        /*
         If we do not fault here, we will notify the user that the sign-out was successful.
         */
        await turnContext.SendActivityAsync("You have signed out", cancellationToken: cancellationToken);
    }

    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (turnContext.Activity.Text == "auto")
        {
            await Authorization.SignInUserAsync(turnContext, turnState, "graph", cancellationToken: cancellationToken); //--> this blocks?

            await turnContext.SendActivityAsync($"Auto Sign In: Successfully logged in to '{Authorization.DefaultHandlerName}', token length: {Authorization.GetTurnToken(Authorization.DefaultHandlerName).Length}", cancellationToken: cancellationToken);
        }
        else
        {
            // Not one of the defined inputs.  Just repeat what user said.

            var a = Authorization.GetTurnToken(Authorization.DefaultHandlerName);

            await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        }
    }

    private async Task OnUserSignInSuccess(ITurnContext turnContext, ITurnState turnState, string handlerName, string token, IActivity initiatingActivity, CancellationToken cancellationToken)
    {
        System.Diagnostics.Trace.WriteLine($"Manual Sign In: Successfully logged OID `{initiatingActivity.From.AadObjectId}` in to '{handlerName}', token length: {token.Length}");
        //await turnContext.SendActivityAsync($"Manual Sign In: Successfully logged in to '{handlerName}'", cancellationToken: cancellationToken);
    }

    private async Task OnUserSignInFailure(ITurnContext turnContext, ITurnState turnState, string handlerName, SignInResponse response, IActivity initiatingActivity, CancellationToken cancellationToken)
    {

        await turnContext.SendActivityAsync($"Manual Sign In: Failed to login to '{handlerName}': {response.Error.Message}", cancellationToken: cancellationToken);
    }
}
