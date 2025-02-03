// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Application.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.Application.AdaptiveCards
{
    /// <summary>
    /// Constants for adaptive card invoke names
    /// </summary>
    public class AdaptiveCardsInvokeNames
    {
        /// <summary>
        /// Action invoke name
        /// </summary>
        public static readonly string ACTION_INVOKE_NAME = "adaptiveCard/action";
    }

    /// <summary>
    /// AdaptiveCards class to enable fluent style registration of handlers related to Adaptive Cards.
    /// </summary>
    /// <typeparam name="TState">The type of the turn state object used by the application.</typeparam>
    public class AdaptiveCards<TState>
        where TState : TurnState, new()
    {
        private static readonly string ACTION_EXECUTE_TYPE = "Action.Execute";
        private static readonly string SEARCH_INVOKE_NAME = "application/search";
        private static readonly string DEFAULT_ACTION_SUBMIT_FILTER = "verb";

        private readonly Application<TState> _app;

        /// <summary>
        /// Creates a new instance of the AdaptiveCards class.
        /// </summary>
        /// <param name="app"></param> The top level application class to register handlers with.
        public AdaptiveCards(Application<TState> app)
        {
            this._app = app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events.
        /// </summary>
        /// <param name="verb">The named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionExecute(string verb, ActionExecuteHandlerAsync<TState> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verb);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = CreateActionExecuteSelector((string input) => string.Equals(verb, input));
            return OnActionExecute(routeSelector, handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events.
        /// </summary>
        /// <param name="verbPattern">Regular expression to match against the named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionExecute(Regex verbPattern, ActionExecuteHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(verbPattern);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = CreateActionExecuteSelector((string input) => verbPattern.IsMatch(input));
            return OnActionExecute(routeSelector, handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionExecute(RouteSelectorAsync routeSelector, ActionExecuteHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            RouteHandler<TState> routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                AdaptiveCardInvokeValue? invokeValue;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, AdaptiveCardsInvokeNames.ACTION_INVOKE_NAME)
                    || (invokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardInvokeValue>(turnContext.Activity.Value)) == null
                    || invokeValue.Action == null
                    || !string.Equals(invokeValue.Action.Type, ACTION_EXECUTE_TYPE))
                {
                    throw new InvalidOperationException($"Unexpected AdaptiveCards.OnActionExecute() triggered for activity type: {turnContext.Activity.Type}");
                }

                AdaptiveCardInvokeResponse adaptiveCardInvokeResponse = await handler(turnContext, turnState, invokeValue.Action.Data, cancellationToken);
                Activity activity = ActivityUtilities.CreateInvokeResponseActivity(adaptiveCardInvokeResponse);
                await turnContext.SendActivityAsync(activity, cancellationToken);
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Execute events.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionExecute(MultipleRouteSelector routeSelectors, ActionExecuteHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnActionExecute(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnActionExecute(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActionExecute(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="verb">The named action to be handled.</param>
        /// <param name="handler">Function to call when the action is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionSubmit(string verb, ActionSubmitHandler<TState> handler)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(verb);
            ArgumentNullException.ThrowIfNull(handler);
            string filter = _app.Options.AdaptiveCards?.ActionSubmitFilter ?? DEFAULT_ACTION_SUBMIT_FILTER;
            RouteSelectorAsync routeSelector = CreateActionSubmitSelector((string input) => string.Equals(verb, input), filter);
            return OnActionSubmit(routeSelector, handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="verbPattern">Regular expression to match against the named action to be handled.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionSubmit(Regex verbPattern, ActionSubmitHandler<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(verbPattern);
            ArgumentNullException.ThrowIfNull(handler);
            string filter = _app.Options.AdaptiveCards?.ActionSubmitFilter ?? DEFAULT_ACTION_SUBMIT_FILTER;
            RouteSelectorAsync routeSelector = CreateActionSubmitSelector((string input) => verbPattern.IsMatch(input), filter);
            return OnActionSubmit(routeSelector, handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionSubmit(RouteSelectorAsync routeSelector, ActionSubmitHandler<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            RouteHandler<TState> routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(turnContext.Activity.Text)
                    || turnContext.Activity.Value == null)
                {
                    throw new InvalidOperationException($"Unexpected AdaptiveCards.OnActionSubmit() triggered for activity type: {turnContext.Activity.Type}");
                }

                await handler(turnContext, turnState, turnContext.Activity.Value, cancellationToken);
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: false);
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card Action.Submit events.
        /// </summary>
        /// <remarks>
        /// The route will be added for the specified verb(s) and will be filtered using the
        /// `actionSubmitFilter` option. The default filter is to use the `verb` field.
        /// 
        /// For outgoing AdaptiveCards you will need to include the verb's name in the cards Action.Submit.
        /// For example:
        ///
        /// ```JSON
        /// {
        ///   "type": "Action.Submit",
        ///   "title": "OK",
        ///   "data": {
        ///     "verb": "ok"
        ///   }
        /// }
        /// ```
        /// </remarks>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnActionSubmit(MultipleRouteSelector routeSelectors, ActionSubmitHandler<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnActionSubmit(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnActionSubmit(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnActionSubmit(routeSelector, handler);
                }
            }
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="dataset">The dataset to be searched.</param>
        /// <param name="handler">Function to call when the search is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnSearch(string dataset, SearchHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(dataset);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = CreateSearchSelector((string input) => string.Equals(dataset, input));
            return OnSearch(routeSelector, handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="datasetPattern">Regular expression to match against the dataset to be searched.</param>
        /// <param name="handler">Function to call when the search is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnSearch(Regex datasetPattern, SearchHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(datasetPattern);
            ArgumentNullException.ThrowIfNull(handler);
            RouteSelectorAsync routeSelector = CreateSearchSelector((string input) => datasetPattern.IsMatch(input));
            return OnSearch(routeSelector, handler);
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="routeSelector">Function that's used to select a route. The function returning true triggers the route.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnSearch(RouteSelectorAsync routeSelector, SearchHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelector);
            ArgumentNullException.ThrowIfNull(handler);
            RouteHandler<TState> routeHandler = async (turnContext, turnState, cancellationToken) =>
            {
                AdaptiveCardSearchInvokeValue? searchInvokeValue;
                if (!string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(turnContext.Activity.Name, SEARCH_INVOKE_NAME)
                    || (searchInvokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardSearchInvokeValue>(turnContext.Activity.Value)) == null)
                {
                    throw new InvalidOperationException($"Unexpected AdaptiveCards.OnSearch() triggered for activity type: {turnContext.Activity.Type}");
                }

                AdaptiveCardsSearchParams adaptiveCardsSearchParams = new(searchInvokeValue.QueryText, searchInvokeValue.Dataset ?? string.Empty);
                Query<AdaptiveCardsSearchParams> query = new(searchInvokeValue.QueryOptions.Top, searchInvokeValue.QueryOptions.Skip, adaptiveCardsSearchParams);
                IList<AdaptiveCardsSearchResult> results = await handler(turnContext, turnState, query, cancellationToken);

                // Check to see if an invoke response has already been added
                if (turnContext.TurnState.Get<object>(ChannelAdapter.InvokeResponseKey) == null)
                {
                    SearchInvokeResponse searchInvokeResponse = new()
                    {
                        StatusCode = 200,
                        Type = "application/vnd.microsoft.search.searchResponse",
                        Value = new AdaptiveCardsSearchInvokeResponseValue
                        {
                            Results = results
                        }
                    };
                    Activity activity = ActivityUtilities.CreateInvokeResponseActivity(searchInvokeResponse);
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                }
            };
            _app.AddRoute(routeSelector, routeHandler, isInvokeRoute: true);
            return _app;
        }

        /// <summary>
        /// Adds a route to the application for handling Adaptive Card dynamic search events.
        /// </summary>
        /// <param name="routeSelectors">Combination of String, Regex, and RouteSelectorAsync selectors.</param>
        /// <param name="handler">Function to call when the route is triggered.</param>
        /// <returns>The application instance for chaining purposes.</returns>
        public Application<TState> OnSearch(MultipleRouteSelector routeSelectors, SearchHandlerAsync<TState> handler)
        {
            ArgumentNullException.ThrowIfNull(routeSelectors);
            ArgumentNullException.ThrowIfNull(handler);
            if (routeSelectors.Strings != null)
            {
                foreach (string verb in routeSelectors.Strings)
                {
                    OnSearch(verb, handler);
                }
            }
            if (routeSelectors.Regexes != null)
            {
                foreach (Regex verbPattern in routeSelectors.Regexes)
                {
                    OnSearch(verbPattern, handler);
                }
            }
            if (routeSelectors.RouteSelectors != null)
            {
                foreach (RouteSelectorAsync routeSelector in routeSelectors.RouteSelectors)
                {
                    OnSearch(routeSelector, handler);
                }
            }
            return _app;
        }

        private static RouteSelectorAsync CreateActionExecuteSelector(Func<string, bool> isMatch)
        {
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                AdaptiveCardInvokeValue? invokeValue;
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, AdaptiveCardsInvokeNames.ACTION_INVOKE_NAME)
                    && (invokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardInvokeValue>(turnContext.Activity.Value)) != null
                    && invokeValue.Action != null
                    && string.Equals(invokeValue.Action.Type, ACTION_EXECUTE_TYPE)
                    && isMatch(invokeValue.Action.Verb));
            };
            return routeSelector;
        }

        private static RouteSelectorAsync CreateActionSubmitSelector(Func<string, bool> isMatch, string filter)
        {
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                JsonObject obj = ProtocolJsonSerializer.ToObject<JsonObject>(turnContext.Activity.Value);
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.Message, StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrEmpty(turnContext.Activity.Text)
                    && turnContext.Activity.Value != null
                    && (obj = turnContext.Activity.Value as JsonObject) != null
                    && obj[filter] != null
                    && obj[filter]!.GetValueKind() == System.Text.Json.JsonValueKind.String
                    && isMatch(obj[filter]!.ToString()!));
            };
            return routeSelector;
        }

        private static RouteSelectorAsync CreateSearchSelector(Func<string, bool> isMatch)
        {
            RouteSelectorAsync routeSelector = (turnContext, cancellationToken) =>
            {
                AdaptiveCardSearchInvokeValue searchInvokeValue = ProtocolJsonSerializer.ToObject<AdaptiveCardSearchInvokeValue>(turnContext.Activity.Value);
                return Task.FromResult(
                    string.Equals(turnContext.Activity.Type, ActivityTypes.Invoke, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(turnContext.Activity.Name, SEARCH_INVOKE_NAME)
                    && (searchInvokeValue != null
                    && isMatch(searchInvokeValue.Dataset!)));
            };
            return routeSelector;
        }

        private class AdaptiveCardSearchInvokeValue : SearchInvokeValue
        {
            public string? Dataset { get; set; }
        }

        private class AdaptiveCardsSearchInvokeResponseValue
        {
            public IList<AdaptiveCardsSearchResult>? Results { get; set; }
        }
    }
}
