// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;

namespace Microsoft.Agents.Core.Telemetry
{
    public static class ActivityExtensions
    {
        public static Activity CloneActivity(this Activity source, bool start = false)
        {
            var clone = new Activity(source.OperationName)
                .SetIdFormat(source.IdFormat);

            if (source.ParentId != null)
            {
                clone.SetParentId(source.ParentId);
            }

            foreach (var tag in source.Tags)
            {
                clone.AddTag(tag.Key, tag.Value);
            }

            foreach (var bag in source.Baggage)
            {
                clone.AddBaggage(bag.Key, bag.Value);
            }

            foreach (var link in source.Links)
            {
                clone.AddLink(link);
            }

            foreach (var eventItem in source.Events)
            {
                clone.AddEvent(eventItem);
            }

            clone.SetStartTime(source.StartTimeUtc);

            if (start)
            {
                clone.Start();
            }

            return clone;
        }
    }
}
