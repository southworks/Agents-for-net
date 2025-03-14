// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Errors;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Microsoft.Agents.BotBuilder.App
{
    public enum RouteType
    {
        Activity,
        Message,
        Conversation,
        BeforeTurn,
        AfterTurn,
        HandOff,
        ReactionAdded,
        ReactionRemoved
    }

    /// <summary>
    /// Adds an AgentApplication Routes
    /// 
    /// Route Type:
    /// <code>
    ///    Activity,       // { ActivityType | RegEx | Selector}, Rank
    ///    Message,        // { ActivityText | RegEx | Selector}, Rank
    ///    Conversation,   // { Event | Selector}, Rank
    ///    BeforeTurn,     // Selector, Rank = order added/defined
    ///    AfterTurn,      // Selector, Rank = order added/defined
    ///    HandOff,        // Selector, Rank
    ///    ReactionAdded,  // Selector, Rank
    ///    ReactionRemoved // Selector, Rank
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class RouteAttribute : Attribute, IRouteAttribute
    {
        public RouteType Type { get; set; }

        public string ActivityType { get; set; }
        public string ActivityText { get; set; }

        public string Regex { get; set; }

        public string Selector { get; set; }

        public string Event { get; set; }


        public ushort Rank { get; set; } = RouteRank.Unspecified;

        public void AddRoute(AgentApplication app, MethodInfo routeMethod)
        {
            if (Type == RouteType.Activity)
            {
                if (!string.IsNullOrWhiteSpace(ActivityType))
                {
                    app.OnActivity(ActivityType, routeMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
                    app.OnActivity(new Regex(Regex), routeMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateDelegates<RouteSelectorAsync, RouteHandler>(app, routeMethod, selectorMethod, out var delegateSelector, out var delegateHandler);

                    app.OnActivity(delegateSelector, delegateHandler, rank: Rank);
                }
            }
            else if (Type == RouteType.Message)
            {
                if (!string.IsNullOrWhiteSpace(ActivityText))
                {
                    app.OnMessage(ActivityText, routeMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Regex))
                {
                    app.OnMessage(new Regex(Regex), routeMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateDelegates<RouteSelectorAsync, RouteHandler>(app, routeMethod, selectorMethod, out var delegateSelector, out var delegateHandler);

                    app.OnMessage(delegateSelector, delegateHandler, rank: Rank);
                }
            }
            else if (Type == RouteType.Conversation)
            {
                if (!string.IsNullOrWhiteSpace(Event))
                {
                    app.OnConversationUpdate(Event, routeMethod.CreateDelegate<RouteHandler>(app), rank: Rank);
                }
                else if (!string.IsNullOrWhiteSpace(Selector))
                {
                    GetSelectorMethodInfo(app, Selector, out var selectorMethod);
                    CreateDelegates<RouteSelectorAsync, RouteHandler>(app, routeMethod, selectorMethod, out var delegateSelector, out var delegateHandler);

                    app.OnConversationUpdate(delegateSelector, delegateHandler, rank: Rank);
                }
            }
        }

        private static void GetSelectorMethodInfo(AgentApplication app, string selectorName, out MethodInfo selectorMethod)
        {
            selectorMethod = app.GetType().GetMethod(selectorName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorNotFound, null);
        }

        private static void CreateDelegates<TDelegate, THandler>(AgentApplication app, MethodInfo routeMethod, MethodInfo selectorMethod, out TDelegate delegateSelector, out THandler delegateHandler) 
            where TDelegate : class, Delegate
            where THandler : class, Delegate
        {
            try
            {
                delegateSelector = selectorMethod.CreateDelegate<TDelegate>(app);
                delegateHandler = routeMethod.CreateDelegate<THandler>(app);
            }
            catch (ArgumentException ex)
            {
                throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorInvalid, ex);
            }
        }
    }
}
