﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Protocols.Primitives;

namespace Microsoft.Agents.Client
{
    /// <summary>
    /// Activity Protocol callback implementation.
    /// </summary>
    public interface IChannelResponseHandler
    {
        /// <summary>
        /// SendToConversation() API.
        /// </summary>
        /// <remarks>
        /// This method allows you to send an activity to the end of a conversation.
        ///
        /// This is slightly different from ReplyToActivity().
        /// * SendToConversation(conversationId) - will append the activity to the end
        /// of the conversation according to the timestamp or semantics of the channel.
        /// * ReplyToActivity(conversationId,ActivityId) - adds the activity as a reply
        /// to another activity, if the channel supports it. If the channel does not
        /// support nested replies, ReplyToActivity falls back to SendToConversation.
        ///
        /// Use ReplyToActivity when replying to a specific activity in the
        /// conversation.
        ///
        /// Use SendToConversation in all other cases.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>conversationId.</param> 
        /// <param name='activity'>Activity to send.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a resource response.</returns>
        Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, Activity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// OnReplyToActivityAsync() API.
        /// </summary>
        /// <remarks>
        /// Override this method allows to reply to an Activity.
        ///
        /// This is slightly different from SendToConversation().
        /// * SendToConversation(conversationId) - will append the activity to the end
        /// of the conversation according to the timestamp or semantics of the channel.
        /// * ReplyToActivity(conversationId,ActivityId) - adds the activity as a reply
        /// to another activity, if the channel supports it. If the channel does not
        /// support nested replies, ReplyToActivity falls back to SendToConversation.
        ///
        /// Use ReplyToActivity when replying to a specific activity in the
        /// conversation.
        ///
        /// Use SendToConversation in all other cases.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='activityId'>activityId the reply is to (OPTIONAL).</param>
        /// <param name='activity'>Activity to send.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a resource response.</returns>
        Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// OnUpdateActivityAsync() API.
        /// </summary>
        /// <remarks>
        /// Override this method to edit a previously sent existing activity.
        ///
        /// Some channels allow you to edit an existing activity to reflect the new
        /// state of a bot conversation.
        ///
        /// For example, you can remove buttons after someone has clicked "Approve"
        /// button.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='activityId'>activityId to update.</param>
        /// <param name='activity'>replacement Activity.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a resource response.</returns>
        Task<ResourceResponse> OnUpdateActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default);

        /// <summary>
        /// OnDeleteActivityAsync() API.
        /// </summary>
        /// <remarks>
        /// Override this method to Delete an existing activity.
        ///
        /// Some channels allow you to delete an existing activity, and if successful
        /// this method will remove the specified activity.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='activityId'>activityId to delete.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a resource response.</returns>
        Task OnDeleteActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// OnGetActivityMembersAsync() API.
        /// </summary>
        /// <remarks>
        /// Override this method to enumerate the members of an activity.
        ///
        /// This REST API takes a ConversationId and a ActivityId, returning an array
        /// of ChannelAccount objects representing the members of the particular
        /// activity in the conversation.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='activityId'>Activity ID.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task with result.</returns>
        Task<IList<ChannelAccount>> OnGetActivityMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, CancellationToken cancellationToken = default);

        /// <summary>
        /// CreateConversation() API.
        /// </summary>
        /// <remarks>
        /// Override this method to create a new Conversation.
        ///
        /// POST to this method with a
        /// * Bot being the bot creating the conversation
        /// * IsGroup set to true if this is not a direct message (default is false)
        /// * Array containing the members to include in the conversation
        ///
        /// The return value is a ResourceResponse which contains a conversation ID
        /// which is suitable for use
        /// in the message payload and REST API URIs.
        ///
        /// Most channels only support the semantics of bots initiating a direct
        /// message conversation.  An example of how to do that would be:
        ///
        /// var resource = await connector.conversations.CreateConversation(new
        /// ConversationParameters(){ Bot = bot, members = new ChannelAccount[] { new
        /// ChannelAccount("user1") } );
        /// await connect.Conversations.OnSendToConversationAsync(resource.Id, new
        /// Activity() ... ) ;
        ///
        /// end. 
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='parameters'>Parameters to create the conversation from.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a conversation resource response.</returns>
        Task<ConversationResourceResponse> OnCreateConversationAsync(ClaimsIdentity claimsIdentity, ConversationParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// OnGetConversationsAsync() API.
        /// </summary>
        /// <remarks>
        /// Override this method to list the Conversations in which this bot has participated.
        /// 
        /// GET from this method with a skip token
        /// 
        /// The return value is a ConversationsResult, which contains an array of
        /// ConversationMembers and a skip token.  If the skip token is not empty, then
        /// there are further values to be returned. Call this method again with the
        /// returned token to get more values.
        /// 
        /// Each ConversationMembers object contains the ID of the conversation and an
        /// array of ChannelAccounts that describe the members of the conversation.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>conversationId.</param> 
        /// <param name='continuationToken'>skip or continuation token.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for ConversationsResult.</returns>
        Task<ConversationsResult> OnGetConversationsAsync(ClaimsIdentity claimsIdentity, string conversationId, string continuationToken = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// GetConversationMembers() API.
        /// </summary>
        /// <remarks>
        /// Override this method to enumerate the members of a conversation.
        ///
        /// This REST API takes a ConversationId and returns an array of ChannelAccount
        /// objects representing the members of the conversation.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a response.</returns>
        Task<IList<ChannelAccount>> OnGetConversationMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// GetConversationMember() API.
        /// </summary>
        /// <remarks>
        /// Override this method to get the account of a single conversation member.
        ///
        /// This REST API takes a ConversationId and UserId and returns the ChannelAccount
        /// objects representing the member of the conversation.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name="userId">User ID.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a response.</returns>
        Task<ChannelAccount> OnGetConversationMemberAsync(ClaimsIdentity claimsIdentity, string userId, string conversationId, CancellationToken cancellationToken = default);

        /// <summary>
        /// GetConversationPagedMembers() API.
        /// </summary>
        /// <remarks>
        /// Override this method to enumerate the members of a conversation one page at a time.
        ///
        /// This REST API takes a ConversationId. Optionally a pageSize and/or
        /// continuationToken can be provided. It returns a PagedMembersResult, which
        /// contains an array
        /// of ChannelAccounts representing the members of the conversation and a
        /// continuation token that can be used to get more values.
        ///
        /// One page of ChannelAccounts records are returned with each call. The number
        /// of records in a page may vary between channels and calls. The pageSize
        /// parameter can be used as
        /// a suggestion. If there are no additional results the response will not
        /// contain a continuation token. If there are no members in the conversation
        /// the Members will be empty or not present in the response.
        ///
        /// A response to a request that has a continuation token from a prior request
        /// may rarely return members from a previous request.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='pageSize'>Suggested page size.</param>
        /// <param name='continuationToken'>Continuation Token.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a response.</returns>
        Task<PagedMembersResult> OnGetConversationPagedMembersAsync(ClaimsIdentity claimsIdentity, string conversationId, int? pageSize = default, string continuationToken = default, CancellationToken cancellationToken = default);

        /// <summary>
        /// DeleteConversationMember() API.
        /// </summary>
        /// <remarks>
        /// Override this method to deletes a member from a conversation.
        ///
        /// This REST API takes a ConversationId and a memberId (of type string) and
        /// removes that member from the conversation. If that member was the last
        /// member
        /// of the conversation, the conversation will also be deleted.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='memberId'>ID of the member to delete from this conversation.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task.</returns>
        Task OnDeleteConversationMemberAsync(ClaimsIdentity claimsIdentity, string conversationId, string memberId, CancellationToken cancellationToken = default);

        /// <summary>
        /// SendConversationHistory() API.
        /// </summary>
        /// <remarks>
        /// Override this method to this method allows you to upload the historic activities to the conversation.
        ///
        /// Sender must ensure that the historic activities have unique ids and
        /// appropriate timestamps. The ids are used by the client to deal with
        /// duplicate activities and the timestamps are used by the client to render
        /// the activities in the right order.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='transcript'>Transcript of activities.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task for a resource response.</returns>
        Task<ResourceResponse> OnSendConversationHistoryAsync(ClaimsIdentity claimsIdentity, string conversationId, Transcript transcript, CancellationToken cancellationToken = default);

        /// <summary>
        /// UploadAttachment() API.
        /// </summary>
        /// <remarks>
        /// 
        /// Override this method to store data in a compliant store when dealing with enterprises.
        /// 
        /// The response is a ResourceResponse which contains an AttachmentId which is
        /// suitable for using with the attachments API.
        /// </remarks>
        /// <param name="claimsIdentity">claimsIdentity for the bot, should have AudienceClaim, AppIdClaim and ServiceUrlClaim.</param>
        /// <param name='conversationId'>Conversation ID.</param>
        /// <param name='attachmentUpload'>Attachment data.</param>
        /// <param name='cancellationToken'>The cancellation token.</param>
        /// <returns>task with result.</returns>
        Task<ResourceResponse> OnUploadAttachmentAsync(ClaimsIdentity claimsIdentity, string conversationId, AttachmentData attachmentUpload, CancellationToken cancellationToken = default);
    }
}
