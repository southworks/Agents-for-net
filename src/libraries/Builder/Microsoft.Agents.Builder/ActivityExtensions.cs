// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder
{
    public static class ActivityExtensions
    {
        public static bool IsAgenticRequest(this IActivity activity)
        {
            return string.Equals(activity?.Recipient?.Role, RoleTypes.AgenticUser, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(activity?.Recipient?.Role, RoleTypes.AgenticIdentity, System.StringComparison.OrdinalIgnoreCase);
        }

        public static string GetAgenticTenantId(this IActivity activity)
        {
            if (!string.IsNullOrEmpty(activity?.Recipient?.TenantId))
            {
                return activity?.Recipient?.TenantId;
            }
            return activity.Conversation?.TenantId;
        }

        public static string GetAgenticInstanceId(this IActivity activity)
        {
            if (!activity.IsAgenticRequest()) return null;
            return activity?.Recipient?.AgenticAppId;
        }

        public static string GetAgenticUser(this IActivity activity)
        {
            if (!activity.IsAgenticRequest()) return null;
            return activity?.Recipient?.AgenticUserId;
        }
    }
}
