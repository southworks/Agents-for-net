// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.App.AdaptiveCards;
using Microsoft.Agents.BotBuilder.App.Authentication;
using Microsoft.Agents.BotBuilder.App.Authentication.TokenService;
using Microsoft.Agents.BotBuilder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.Agents.BotBuilder.App.Authentication.AuthenticationManager;

namespace Microsoft.Agents.BotBuilder.App
{
    /// <summary>
    /// Application class for routing and processing incoming requests.
    /// </summary>
    public class Application : IBot
    {
        private readonly AuthenticationManager _authentication;

        private readonly int _typingTimerDelay = 1000;
        private TypingTimer? _typingTimer;

        // TODO:  These really aren't queues, so why this type?
        private readonly ConcurrentQueue<Route> _invokeRoutes;
        private readonly ConcurrentQueue<Route> _routes;
        private readonly ConcurrentQueue<TurnEventHandlerAsync> _beforeTurn;
        private readonly ConcurrentQueue<TurnEventHandlerAsync> _afterTurn;

        private readonly SelectorAsync? _startSignIn;

        /// <summary>
        /// Creates a new Application instance.
        /// </summary>
        /// <param name="options">Optional. Options used to configure the application.</param>
        /// <param name="state"></param>
        public Application(ApplicationOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            Options = options;

            if (Options.TurnStateFactory == null)
            {
                // This defaults to a TurnState with TempState
                Options.TurnStateFactory = () => new TurnState();
            }

            AdaptiveCards = new AdaptiveCardsFeature(this);

            _routes = new ConcurrentQueue<Route>();
            _invokeRoutes = new ConcurrentQueue<Route>();
            _beforeTurn = new ConcurrentQueue<TurnEventHandlerAsync>();
            _afterTurn = new ConcurrentQueue<TurnEventHandlerAsync>();

            if (options.Authentication != null)
            {
                _authentication = new AuthenticationManager(this, options.Authentication);

                if (options.Authentication.AutoSignIn != null)
                {
                    _startSignIn = options.Authentication.AutoSignIn;
                }
                else
                {
                    // If AutoSignIn wasn't specified, default to true. 
                    _startSignIn = (context, cancellationToken) => Task.FromResult(true);
                }
            }
        }

        /// <summary>
        /// Fluent interface for accessing Adaptive Card specific features.
        /// </summary>
        public AdaptiveCardsFeature AdaptiveCards { get; }

        /// <summary>
        /// Accessing authentication specific features.
        /// </summary>
        public AuthenticationManager Authentication
        {
            get
            {
                if (_authentication == null)
                {
                    throw new ArgumentException("The Application.Authentication property is unavailable because no authentication options were configured.");
                }

                return _authentication;
            }
        }

        /// <summary>
        /// The application's configured options.
        /// </summary>
        public ApplicationOptions Options { get; }

        #region Route Handling
        /// <summary>
        /// Adds a new route to the application.
        /// 
        /// Developers won't typically need to call this method directly as it's used internally by all
        /// of the fluent interfaces to register routes for their specific activity types.
        /// 
        /// Routes will be matched in the order they're added to the application. The first selector to
        /// return `true` when an activity is received will have its handler called.
        ///
        /// Invoke-based activities receive special treatment and are matched separately as they typically
        /// have shorter execution timeouts.
        /// </summary>
        /// <param name="selector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <param name="isInvokeRoute">Boolean indicating if the RouteSelectorAsync is for an activity that uses "invoke" which require special handling. Defaults to `false`.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application AddRoute(RouteSelectorAsync selector, RouteHandler handler, bool isInvokeRoute = false)
        {
            ArgumentNullException.ThrowIfNull(selector);
            ArgumentNullException.ThrowIfNull(handler);
            Route route = new(selector, handler, isInvokeRoute);
            if (isInvokeRoute)
            {
                _invokeRoutes.Enqueue(route);
            }
            else
            {
                _routes.Enqueue(route);
            }
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="type">Name of the activity type to match.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnActivity(string type, RouteHandler handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(type);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult(string.Equals(type, context.Activity?.Type, StringComparison.OrdinalIgnoreCase));
            OnActivity(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="typePattern">Regular expression to match against the incoming activity type.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnActivity(Regex typePattern, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(typePattern);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult(context.Activity?.Type != null && typePattern.IsMatch(context.Activity?.Type));
            OnActivity(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnActivity(RouteSelectorAsync routeSelector, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles incoming activities of a given type.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnActivity(MultipleRouteSelector routeSelectors, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string type in routeSelectors.Strings)
                {
                    OnActivity(type, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex typePattern in routeSelectors.Regexes)
                {
                    OnActivity(typePattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActivity(routeSelector, handler);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvent">Name of the conversation update event to handle, can use <see cref="ConversationUpdateEvents"/>.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public virtual Application OnConversationUpdate(string conversationUpdateEvent, RouteHandler handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(conversationUpdateEvent);
            ArgumentNullException.ThrowIfNull(handler);

            RouteSelectorAsync routeSelector;
            switch (conversationUpdateEvent)
            {
                case ConversationUpdateEvents.MembersAdded:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && context.Activity?.MembersAdded != null
                            && context.Activity.MembersAdded.Count > 0
                        );
                        break;
                    }
                case ConversationUpdateEvents.MembersRemoved:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                            && context.Activity?.MembersRemoved != null
                            && context.Activity.MembersRemoved.Count > 0
                        );
                        break;
                    }
                default:
                    {
                        routeSelector = (context, _) => Task.FromResult
                        (
                            string.Equals(context.Activity?.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase)
                        );
                        break;
                    }
            }
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles conversation update events.
        /// </summary>
        /// <param name="conversationUpdateEvents">Name of the conversation update events to handle, can use <see cref="ConversationUpdateEvents"/> as array item.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnConversationUpdate(string[] conversationUpdateEvents, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(conversationUpdateEvents);
            ArgumentNullException.ThrowIfNull(handler);
            foreach (string conversationUpdateEvent in conversationUpdateEvents)
            {
                OnConversationUpdate(conversationUpdateEvent, handler);
            }
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage("/reset", (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="text">Substring of the incoming message text.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnMessage(string text, RouteHandler handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && context.Activity.Text.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                );
            OnMessage(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// <br/>
        /// For example, you can easily clear the current conversation anytime a user sends "/reset":
        /// <br/>
        /// <code>application.OnMessage(new Regex("reset"), (context, turnState, _) => ...);</code>
        /// </summary>
        /// <param name="textPattern">Regular expression to match against the text of an incoming message.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnMessage(Regex textPattern, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(textPattern);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _)
                => Task.FromResult
                (
                    string.Equals(ActivityTypes.Message, context.Activity?.Type, StringComparison.OrdinalIgnoreCase)
                    && context.Activity?.Text != null
                    && textPattern.IsMatch(context.Activity.Text)
                );
            OnMessage(routeSelector, handler);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnMessage(RouteSelectorAsync routeSelector, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles incoming messages with a given keyword.
        /// <br/>
        /// This method provides a simple way to have a bot respond anytime a user sends your bot a
        /// message with a specific word or phrase.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnMessage(MultipleRouteSelector routeSelectors, RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string text in routeSelectors.Strings)
                {
                    OnMessage(text, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex textPattern in routeSelectors.Regexes)
                {
                    OnMessage(textPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnMessage(routeSelector, handler);
                }
            }
            return this;
        }

        /// <summary>
        /// Handles message reactions added events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnMessageReactionsAdded(RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.MessageReaction, StringComparison.OrdinalIgnoreCase)
                && context.Activity?.ReactionsAdded != null
                && context.Activity.ReactionsAdded.Count > 0
            );
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles message reactions removed events.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnMessageReactionsRemoved(RouteHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.MessageReaction, StringComparison.OrdinalIgnoreCase)
                && context.Activity?.ReactionsRemoved != null
                && context.Activity.ReactionsRemoved.Count > 0
            );
            AddRoute(routeSelector, handler, isInvokeRoute: false);
            return this;
        }

        /// <summary>
        /// Handles handoff activities.
        /// </summary>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnHandoff(HandoffHandler handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = (context, _) => Task.FromResult
            (
                string.Equals(context.Activity?.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                && string.Equals(context.Activity?.Name, "handoff/action")
            );
            RouteHandler routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                string token = turnContext.Activity.Value.GetType().GetProperty("Continuation").GetValue(turnContext.Activity.Value) as string ?? "";
                await handler(turnContext, turnState, token, cancellationToken);

                Activity activity = ActivityUtilities.CreateInvokeResponseActivity();
                await turnContext.SendActivityAsync(activity, cancellationToken);
            };
            AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute before the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to continue execution of the current turn. Handler returning false
        /// prevents the turn from running, but the bots state is still saved, which lets you
        /// track the reason why the turn was not processed. It also means you can use this as
        /// a way to call into the dialog system. For example, you could use the OAuthPrompt to sign the
        /// user in before allowing the AI system to run.
        /// </summary>
        /// <param name="handler">Function to call before turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnBeforeTurn(TurnEventHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _beforeTurn.Enqueue(handler);
            return this;
        }

        /// <summary>
        /// Add a handler that will execute after the turn's activity handler logic is processed.
        /// <br/>
        /// Handler returns true to finish execution of the current turn. Handler returning false
        /// prevents the bots state from being saved.
        /// </summary>
        /// <param name="handler">Function to call after turn execution.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application OnAfterTurn(TurnEventHandlerAsync handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            _afterTurn.Enqueue(handler);
            return this;
        }

        #endregion

        #region ShowTyping
        /// <summary>
        /// Manually start a timer to periodically send "typing" activities.
        /// </summary>
        /// <remarks>
        /// The timer waits 1000ms to send its initial "typing" activity and then send an additional
        /// "typing" activity every 1000ms.The timer will automatically end once an outgoing activity
        /// has been sent. If the timer is already running or the current activity is not a "message"
        /// the call is ignored.
        /// </remarks>
        /// <param name="turnContext">The turn context.</param>
        public void StartTypingTimer(ITurnContext turnContext)
        {
            if (turnContext.Activity.Type != ActivityTypes.Message)
            {
                return;
            }

            if (_typingTimer == null)
            {
                _typingTimer = new TypingTimer(_typingTimerDelay);
            }

            if (!_typingTimer.IsRunning())
            {
                _typingTimer.Start(turnContext);
            }
        }

        /// <summary>
        /// Manually stop the typing timer.
        /// </summary>
        /// <remarks>
        /// If the timer isn't running nothing happens.
        /// </remarks>
        public void StopTypingTimer()
        {
            _typingTimer?.Dispose();
            _typingTimer = null;
        }

        #endregion

        #region User Authentication

        /// <summary>
        /// If the user is signed in, get the access token. If not, triggers the sign in flow for the provided authentication setting name
        /// and returns.In this case, the bot should end the turn until the sign in flow is completed.
        /// </summary>
        /// <remarks>
        /// Use this method to get the access token for a user that is signed in to the bot.
        /// If the user isn't signed in, this method starts the sign-in flow.
        /// The bot should end the turn in this case until the sign-in flow completes and the user is signed in.
        /// </remarks>
        /// <param name="turnContext"> The turn context.</param>
        /// <param name="turnState"></param>
        /// <param name="settingName">The name of the authentication setting.</param>
        /// <param name="completionHandler"></param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <exception cref="InvalidOperationException"></exception>
        public Task GetTokenOrStartSignInAsync(ITurnContext turnContext, ITurnState turnState, string settingName, SignInCompletionHandlerAsync completionHandler, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();

            /*
            // TODO: AddRoute

            // User is currently not in sign in flow
            if (AuthUtilities.UserInSignInFlow(turnState) == null)
            {
                AuthUtilities.SetUserInSignInFlow(turnState, settingName);
            }
            else
            {
                AuthUtilities.DeleteUserInSignInFlow(turnState);
                throw new InvalidOperationException("Invalid sign in flow state. Cannot start sign in when already started");
            }

            SignInResponse response = await Authentication.SignUserInAsync(turnContext, settingName, cancellationToken).ConfigureAwait(false);

            if (response.Status == SignInStatus.Error)
            {
                string message = response.Error!.ToString();

                // TODO: the current activity shouldn't trigger an error in the case of manual signin.  Problem is, the flow impls
                // currently ignore all but specific activities.
                if (response.Cause == AuthExceptionReason.InvalidActivity)
                {
                    message = $"User is not signed in and cannot start sign in flow for this activity: {response.Error}";
                }

                throw new InvalidOperationException($"Error occurred while trying to authenticate user: {message}, flow: {settingName}");
            }

            // Call the handler immediately if the user was already signed in.
            if (response.Status == SignInStatus.Complete)
            {
                AuthUtilities.DeleteUserInSignInFlow(turnState);
                AuthUtilities.SetTokenInState(turnState, settingName, response.TokenResponse.Token);
                await completionHandler(turnContext, turnState, settingName, response, cancellationToken).ConfigureAwait(false);
            }

            */
        }

        #endregion

        #region Turn Handling
        /// <summary>
        /// Called by the adapter (for example, a <see cref="CloudAdapter"/>)
        /// at runtime in order to process an inbound <see cref="Activity"/>.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(turnContext);
            ArgumentNullException.ThrowIfNull(turnContext.Activity);

            await _OnTurnAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Internal method to wrap the logic of handling a bot turn.
        /// </summary>
        private async Task _OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken)
        {
            try
            {
                // Start typing timer if configured
                if (Options.StartTypingTimer)
                {
                    StartTypingTimer(turnContext);
                };

                // Remove @mentions
                if (Options.RemoveRecipientMention && ActivityTypes.Message.Equals(turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: What about normalizing mentions?

                    turnContext.Activity.Text = turnContext.Activity.RemoveRecipientMention();
                }

                // Load turn state
                ITurnState turnState = Options.TurnStateFactory!();
                await turnState!.LoadStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                // Handle user auth
                if (_authentication != null)
                {
                    // If a flow is active, continue that.
                    string? flowName = AuthUtilities.UserInSignInFlow(turnState);
                    bool shouldStartSignIn = _startSignIn != null && await _startSignIn(turnContext, cancellationToken);

                    if (shouldStartSignIn || flowName != null)
                    {
                        if (flowName == null)
                        {
                            // Auth flow hasn't start yet.
                            flowName = _authentication.Default;

                            // Bank the Activity so it can be executed after sign in is complete.
                            AuthUtilities.SetUserSigninActivity(turnContext, turnState);
                        }

                        SignInResponse response = await _authentication.SignUserInAsync(turnContext, flowName, cancellationToken: cancellationToken).ConfigureAwait(false);

                        if (response.Status == SignInStatus.Pending)
                        {
                            // Requires user action, save state and stop processing current activity.  Done with this turn.
                            AuthUtilities.SetUserInSignInFlow(turnState, flowName);
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                            return;
                        }

                        if (response.Status == SignInStatus.Error && response.Cause != AuthExceptionReason.InvalidActivity)
                        {
                            await _authentication.Get(flowName).ResetStateAsync(turnContext, cancellationToken).ConfigureAwait(false);
                            AuthUtilities.DeleteUserInSignInFlow(turnState);
                            AuthUtilities.DeleteUserSigninActivity(turnState);
                            await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                            if (Options.Authentication.SignInFailedMessage == null)
                            {
                                throw response.Error;
                            }

                            await turnContext.SendActivitiesAsync(Options.Authentication.SignInFailedMessage(flowName, response), cancellationToken).ConfigureAwait(false);
                            return;
                        }

                        if (response.Status == SignInStatus.Complete)
                        {
                            AuthUtilities.DeleteUserInSignInFlow(turnState);
                            AuthUtilities.SetTokenInState(turnState, flowName, response.TokenResponse.Token);
                            // TODO: should probably call "_authentication.Get(flowName).ResetState?", but is this safe?

                            var signInActivity = AuthUtilities.DeleteUserSigninActivity(turnState);
                            if (signInActivity != null)
                            {
                                // If the current activity matches the one used to trigger sign in, then
                                // this is because the user received a token that didn't involve a multi-turn
                                // flow.  No further action needed.
                                if (!ProtocolJsonSerializer.Equals(signInActivity, turnContext.Activity))
                                {
                                    // Since we could be handling an Invoke in this turn, and ITurnContext.Activity is readonly,
                                    // we need to continue the conversation in a different turn with the original Activity that triggered sign in.
                                    await turnState.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
                                    await Options.Adapter.ProcessProactiveAsync(turnContext.Identity, signInActivity, this, cancellationToken).ConfigureAwait(false);
                                    return;
                                }
                            }

                            if (Options.Authentication.CompletedMessage != null)
                            {
                                await turnContext.SendActivitiesAsync(Options.Authentication.CompletedMessage(flowName, response), cancellationToken).ConfigureAwait(false);
                            }
                        }

                        // If we got this far, fall through to normal Activity route handling.
                    }
                }

                // Call before turn handler
                foreach (TurnEventHandlerAsync beforeTurnHandler in _beforeTurn)
                {
                    if (!await beforeTurnHandler(turnContext, turnState, cancellationToken))
                    {
                        // Save turn state
                        // - This lets the bot keep track of why it ended the previous turn. It also
                        //   allows the dialog system to be used before the AI system is called.
                        await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);

                        return;
                    }
                }

                // Download any input files
                IList<IInputFileDownloader>? fileDownloaders = this.Options.FileDownloaders;
                if (fileDownloaders != null && fileDownloaders.Count > 0)
                {
                    foreach (IInputFileDownloader downloader in fileDownloaders)
                    {
                        var files = await downloader.DownloadFilesAsync(turnContext, turnState, cancellationToken).ConfigureAwait(false);
                        turnState.Temp.InputFiles = [.. turnState.Temp.InputFiles, .. files];
                    }
                }

                bool eventHandlerCalled = false;

                // TODO: why is this needed?  Would not the selector be limiting to "Invoke" anyway, so iterating _routes would be the same thing.
                // Run any RouteSelectors in this._invokeRoutes first if the incoming Teams activity.type is "Invoke".
                // Invoke Activities from Teams need to be responded to in less than 5 seconds.
                if (ActivityTypes.Invoke.Equals(turnContext.Activity.Type, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Route route in _invokeRoutes)
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            await route.Handler(turnContext, turnState, cancellationToken);
                            eventHandlerCalled = true;
                            break;
                        }
                    }
                }

                // All other ActivityTypes and any unhandled Invokes are run through the remaining routes.
                if (!eventHandlerCalled)
                {
                    foreach (Route route in _routes)
                    {
                        if (await route.Selector(turnContext, cancellationToken))
                        {
                            await route.Handler(turnContext, turnState, cancellationToken);
                            eventHandlerCalled = true;
                            break;
                        }
                    }
                }

                // Call after turn handler
                foreach (TurnEventHandlerAsync afterTurnHandler in _afterTurn)
                {
                    if (!await afterTurnHandler(turnContext, turnState, cancellationToken))
                    {
                        return;
                    }
                }

                await turnState!.SaveStateAsync(turnContext, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Stop the timer if configured
                StopTypingTimer();
            }
        }

        #endregion
    }
}
