using Microsoft.Agents.Core.Telemetry;
using System;
using System.Diagnostics;

#nullable enable

namespace Microsoft.Agents.Connector.Telemetry.Scopes
{
    internal class ScopeGetAttachment : ScopeConnectorRequest
    {
        private readonly string _attachmentId;
        private readonly string _viewId;

        public ScopeGetAttachment(string attachmentId, string viewId) : base(Constants.ScopeGetAttachment)
        {
            _attachmentId = attachmentId;
            _viewId = viewId;
        }

        protected override void Callback(Activity activity, double duration, Exception? exception)
        {
            base.Callback(activity, duration, exception);
            activity.SetTag(TagNames.AttachmentId, _attachmentId);
            activity.SetTag(TagNames.ViewId, _viewId);
        }
    }
}
