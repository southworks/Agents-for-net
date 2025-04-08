// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Core.Models;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace AuthorizationAgent;

public class AuthAgent : AgentApplication
{
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

        ///*
        // Handles the user sending a Login or LogOut command using the specific keywords '-signin' and '-signout'
        //*/
        OnMessage("-signin", SignInAsync);
//        OnMessage("-signout", SignOutAsync);

        OnMessage("-signin", SignInAsync);
//        OnMessage("-signout", SignOutAsync);

        UserAuthorization.OnUserSignInSuccess(OnUserSignInSuccess);
        Authorization.OnUserSignInSuccess(OnUserSignInSuccess);
        UserAuthorization.OnUserSignInFailure(OnUserSignInFailure);

        OnActivity(ActivityTypes.Message, OnMessageAsync, rank: RouteRank.Last);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                string displayName = await GetDisplayName(turnContext);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Welcome to the AutoSignIn Example, **{displayName}**!.");
                sb.AppendLine("This Agent automatically signs you in when you first connect.");
                sb.AppendLine("You can use the following commands to interact with the agent:");
                sb.AppendLine("-signout: Sign out of the agent and force it to reset the login flow on next message.");
                if ( displayName.Equals(_defaultDisplayName))
                {
                    sb.AppendLine("**WARNING: We were unable to get your display name with the current access token.. please use the -signout command before proceeding**");
                }
                sb.AppendLine("");
                sb.AppendLine("Type anything else to see the agent echo back your message.");
                await turnContext.SendActivityAsync(MessageFactory.Text(sb.ToString()), cancellationToken);
                sb.Clear(); 
            }
        }
    }

    /// <summary>
    /// Handles general message loop. 
    /// </summary>
    /// <param name="turnContext"><see cref="ITurnContext"/></param>
    /// <param name="turnState"><see cref="ITurnState"/></param>
    /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
    /// <returns></returns>
    private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        /*
        When Auto Sign in is properly configured, the user will be automatically signed in when they first connect to the agent using the default handler chosen in the UserAuthorization configuration.
        IMPORTANT: The ReadMe associated with this sample, instructs you on confiuring the Azure Bot Service Registration with the scopes to allow you to read your own information from Graph.  you must have completed that for this sample to work correctly. 

        If the sign in process is successful, the user will be signed in and the token will be available from the Authorization.GetTurnToken(Authorization.DefaultHandlerName) function call. 
        if the sign in was not successful,  you will get a Null when you call the Authorization.GetTurnToken(Authorization.DefaultHandlerName) function. 
        */

        // Check for Access Token from the Authorization Sub System. 
        if (string.IsNullOrEmpty(Authorization.GetTurnToken(Authorization.DefaultHandlerName)))
        {
            // Failed to get access token here, and we will now bail out of this message loop. 
            await turnContext.SendActivityAsync($"The auto sign in process failed and no access token is available", cancellationToken: cancellationToken);
            return; 
        }

        // We have the access token, now try to get your user name from graph. 
        string displayName = await GetDisplayName(turnContext); 
        if (displayName.Equals(_defaultDisplayName))
        {
            // Handle error response from Graph API
            await turnContext.SendActivityAsync($"Failed to get user information from Graph API \nDid you update the scope correctly in Azure bot Service?. If so type in -signout to force signout the current user", cancellationToken: cancellationToken);
            return;
        }

        // Now Echo back what was said with your display name. 
        await turnContext.SendActivityAsync($"**{displayName} said:** {turnContext.Activity.Text}", cancellationToken: cancellationToken);


        //if (turnContext.Activity.Text == "auto")
        //{
        //    await Authorization.SignInUserAsync(turnContext, turnState, "graph", cancellationToken: cancellationToken); //--> this blocks?

        //    await turnContext.SendActivityAsync($"Auto Sign In: Successfully logged in to '{Authorization.DefaultHandlerName}', token length: {Authorization.GetTurnToken(Authorization.DefaultHandlerName).Length}", cancellationToken: cancellationToken);
        //}
        //else
        //{
        //    // Not one of the defined inputs.  Just repeat what user said.

        //    var a = Authorization.GetTurnToken(Authorization.DefaultHandlerName);

        //    await turnContext.SendActivityAsync($"You said: {turnContext.Activity.Text}", cancellationToken: cancellationToken);
        //}
    }

    private async Task<string> GetDisplayName(ITurnContext turnContext)
    {
        string displayName = _defaultDisplayName;
        string accessToken = Authorization.GetTurnToken(Authorization.DefaultHandlerName);
        string graphApiUrl = $"https://graph.microsoft.com/v1.0/me";
        HttpClient client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage response = await client.GetAsync(graphApiUrl);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            dynamic graphResponse = JObject.Parse(content);
            displayName = graphResponse.displayName;
        }
        return displayName; 
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
