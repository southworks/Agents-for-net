using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetAttachmentInfo : ScopeConnectorRequest
    {
        private readonly string _attachmentId;

        public ScopeGetAttachmentInfo(string attachmentId) : base(Constants.ScopeGetAttachmentInfo)
        {
            _attachmentId = attachmentId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            base.Callback(activity, duration, exception);
            activity.SetTag(TagNames.AttachmentId, _attachmentId);
        }
    }
}
