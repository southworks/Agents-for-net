using AgentNotification.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Kairo.Sdk.AgentsSdkExtensions.Models
{
    public class AgentNotificationActivity
    {
        public WpxComment? WpxCommentNotification { get; set; }
        public EmailReference? EmailNotification { get; set; }
        public NotificationTypeEnum NotificationType { get; set; } = NotificationTypeEnum.Unknown;

        public AgentNotificationActivity(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));
            if ( activity.Entities != null && activity.Entities.Count > 0)
            {
                var wpxCommentEntity = activity.Entities.FirstOrDefault(e => e.Type == nameof(WpxComment));
                if (wpxCommentEntity != null)
                {
                    WpxCommentNotification = ProtocolJsonSerializer.ToObject<WpxComment>(wpxCommentEntity) ?? new();
                    NotificationType = NotificationTypeEnum.WpxComment;
                }
                var emailEntity = activity.Entities.FirstOrDefault(e => e.Type == EmailReference.EntityTypeName);
                if (emailEntity != null)
                {
                    EmailNotification = ProtocolJsonSerializer.ToObject<EmailReference>(emailEntity) ?? new();
                    NotificationType = NotificationTypeEnum.EmailNotification;
                }
            }
        }
    }
}
