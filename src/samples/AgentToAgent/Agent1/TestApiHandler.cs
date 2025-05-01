using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Agent1
{
    public class TestApiHandler(ILogger logger) : IChannelApiHandler
    {
        public Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize = null, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken = null, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("OnReplyToActivityAsync");
            return null;
        }

        public Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("OnSendToConversationAsync");
            return null;
        }

        public Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, IActivity activity, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
