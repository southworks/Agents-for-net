using Microsoft.Agents.Core.Models;
using Microsoft.Kairo.Sdk.AgentsSdkExtensions.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Kairo.Sdk.AgentsSdkExtensions
{
    public static class ActivityExtension
    {
        public static EmailReference? GetEmailReference(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }

            var entity = activity.Entities.FirstOrDefault(e => string.Equals(e.Type, "emailnotification", StringComparison.OrdinalIgnoreCase));
            return entity?.GetAs<EmailReference>();
        }
        
        public static WpxComment? GetWpxComment(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }
            var entity = activity.Entities.FirstOrDefault(e => string.Equals(e.Type, "wpxcomment", StringComparison.OrdinalIgnoreCase));
            return entity?.GetAs<WpxComment>();
        }

        public static AgentNotificationActivity GetAgentNotificationActivity(this IActivity activity)
        {
            return new AgentNotificationActivity(activity);
        }
    }
}
