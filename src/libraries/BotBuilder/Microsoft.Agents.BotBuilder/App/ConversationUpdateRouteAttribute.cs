// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder.Errors;
using System;
using System.Reflection;

namespace Microsoft.Agents.BotBuilder.App
{
    /// <summary>
    /// Adds an AgentApplication.OnConversation route.
    /// Only one of will be used:
    /// 1. Event
    /// 3. Selector
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public class ConversationUpdateRouteAttribute : Attribute, IRouteAttribute
    {
        public string Event { get; set; }

        public string Selector { get; set; }

        public ushort Rank { get; set; } = RouteRank.Unspecified;

        public void AddRoute(AgentApplication app, MethodInfo method)
        {
            if (!string.IsNullOrWhiteSpace(Event))
            {
                app.OnConversationUpdate(Event, method.CreateDelegate<RouteHandler>(app), Rank);
            }
            else if (!string.IsNullOrWhiteSpace(Selector))
            {
                var selectorMethod = app.GetType().GetMethod(Selector, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorNotFound, null);

                try
                {
                    var delegateSelector = selectorMethod.CreateDelegate<RouteSelectorAsync>(app);
                    var delegateHandler = method.CreateDelegate<RouteHandler>(app);
                    app.OnActivity(delegateSelector, delegateHandler, rank: Rank);
                }
                catch (ArgumentException ex)
                {
                    throw Core.Errors.ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.AttributeSelectorInvalid, ex);
                }
            }
        }
    }
}
