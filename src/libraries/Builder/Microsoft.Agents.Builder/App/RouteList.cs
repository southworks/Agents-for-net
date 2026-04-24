// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Microsoft.Agents.Builder.App
{
    internal class RouteList
    {
        private readonly ReaderWriterLockSlim rwl = new();
        private List<RouteEntry> routes = [];

        public void AddRoute(Route route)
        {
            if (rwl.TryEnterWriteLock(1000))
            {
                try
                {
                    routes.Add(new RouteEntry(route));

                    // Ordered by:
                    //    Agentic + Invoke
                    //    Invoke
                    //    Agentic
                    //    Other
                    // Then by Rank
                    routes = [.. routes
                        .OrderByDescending(entry => entry.Order)
                        .ThenBy(entry => entry.Route.Rank)];
                }
                finally
                {
                    rwl.ExitWriteLock();
                }
            }
            else            
            {
                throw new TimeoutException("Failed to acquire write lock to add route.");
            }
        }

        public IEnumerable<Route> Enumerate()
        {
            if (rwl.TryEnterReadLock(1000))
            {
                try
                {
                    return [.. routes.Select(e => e.Route)];
                }
                finally
                {
                    rwl.ExitReadLock();
                }
            }
            else
            {
                throw new TimeoutException("Failed to acquire read lock to enumerate routes.");
            }
        }

        internal int Count
        {
            get
            {
                if (rwl.TryEnterReadLock(1000))
                {
                    try
                    {
                        return routes.Count;
                    }
                    finally
                    {
                        rwl.ExitReadLock();
                    }
                }
                else
                {
                    throw new TimeoutException("Failed to acquire read lock to count routes.");
                }
            }
        }

        internal string FormatRouteList()
        {
            var orderedRoutes = Enumerate();
            var sb = new StringBuilder();
            int index = 0;
            foreach (var route in orderedRoutes)
            {
                // Build flags string manually — RouteFlags has no [Flags] attribute
                var parts = new List<string>();
                if (route.Flags.HasFlag(RouteFlags.Invoke)) parts.Add("Invoke");
                if (route.Flags.HasFlag(RouteFlags.Agentic)) parts.Add("Agentic");
                if (route.Flags.HasFlag(RouteFlags.NonTerminal)) parts.Add("NonTerminal");
                var flagsStr = parts.Count > 0 ? string.Join(",", parts) : "None";

                sb.Append($"  [{index}] {route.Handler.Method.Name} flags={flagsStr} rank={route.Rank}");

                var channel = route.ChannelId?.Channel;
                if (!string.IsNullOrEmpty(channel))
                {
                    sb.Append($" channel={channel}");
                }

                sb.AppendLine();
                index++;
            }

            // Remove trailing newline
            if (sb.Length > 0 && sb[sb.Length - 1] == '\n')
            {
                sb.Length -= sb.Length > 1 && sb[sb.Length - 2] == '\r' ? 2 : 1;
            }

            return sb.ToString();
        }
    }

    enum RouteEntryOrder
    {
        Other = 0,
        Agentic = 1,
        Invoke = 2,
        AgenticInvoke = 3
    }

    class RouteEntry
    {
        public RouteEntry(Route route) 
        { 
            Route = route;
            if (route.Flags.HasFlag(RouteFlags.Invoke))
                Order = RouteEntryOrder.Invoke;
            if (route.Flags.HasFlag(RouteFlags.Agentic))
                Order |= RouteEntryOrder.Agentic;
        }

        public Route Route { get; private set; }
        public RouteEntryOrder Order { get; private set; }
    }
}
