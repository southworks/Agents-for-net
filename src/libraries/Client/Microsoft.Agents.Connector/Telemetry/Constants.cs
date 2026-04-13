using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.Telemetry
{
    internal static class Constants
    {
        internal static readonly string ScopeReplyToActivity = "agents.connector.reply_to_activity";
        internal static readonly string ScopeSendToConversation = "agents.connector.send_to_conversation";
        internal static readonly string ScopeUpdateActivity = "agents.telemetry.update_activity";
        internal static readonly string ScopeDeleteActivity = "agents.telemetry.delete_activity";
        internal static readonly string ScopeCreateConversation = "agents.connector.create_conversation";
        internal static readonly string ScopeGetConversations = "agents.connector.get_conversations";
        internal static readonly string ScopeGetConversationMembers = "agents.connector.get_conversation_members";
        internal static readonly string ScopeUploadAttachment = "agents.connector.upload_attachment";
        internal static readonly string ScopeGetAttachment = "agents.connector.get_attachment";
        internal static readonly string ScopeGetAttachmentInfo = "agents.connector.get_attachment_info";

        internal static readonly string ScopeGetToken = "agents.user.user_token_client.get_user_token";
        internal static readonly string ScopeSignOut = "agents.user_token_client.sign_out";
        internal static readonly string ScopeGetSignInResource = "agents.user_token_client.get_sign_in_resource";
        internal static readonly string ScopeExchangeToken = "agents.user_token_client.exchange_token";
        internal static readonly string ScopeGetTokenOrSignInResource = "agents.user_token_client.get_token_or_sign_in_resource";
        internal static readonly string ScopeGetTokenStatus = "agents.user_token_client.get_token_status";
        internal static readonly string ScopeGetAadTokens = "agents.user_token_client.get_aad_tokens";

        internal static readonly string MetricConnectorRequestCount = "agents.connector.request.count";
        internal static readonly string MetricConnectorRequestDuration = "agents.connector.request.duration";
        
        internal static readonly string MetricUserTokenRestClientRequestCount = "agents.connector.user_token_client.request.count";
        internal static readonly string MetricUserTokenRestClientRequestDuration = "agents.connector.user_token_client.request.duration";
    }
}
