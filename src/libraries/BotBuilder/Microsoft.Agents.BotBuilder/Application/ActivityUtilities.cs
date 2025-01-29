using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System.Net;

namespace Microsoft.Teams.AI
{
    internal static class ActivityUtilities
    {
        public static T? GetTypedValue<T>(IActivity activity)
        {
            return ProtocolJsonSerializer.ToObject<T>(activity.Value);
        }

        public static Activity CreateInvokeResponseActivity(object? body = default)
        {
            Activity activity = new()
            {
                Type = ActivityTypes.InvokeResponse,
                Value = new InvokeResponse { Status = (int)HttpStatusCode.OK, Body = body }
            };
            return activity;
        }
    }
}
