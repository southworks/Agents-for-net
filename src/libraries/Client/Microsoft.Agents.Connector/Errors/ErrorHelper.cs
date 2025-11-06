// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Connector.Errors
{
    /// <summary>
    /// Error helper for the Agent SDK core system
    /// This is used to setup the localized error codes for the AgentSDK
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are is 2 methods in used in the general space. 
    /// Method 1: 
    /// Throw a new exception with the error code, description and helplink
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    ///
    /// Method 2: 
    /// 
    ///     throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException&lt;OperationCanceledException&gt;(
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
    /// </summary>
    internal static partial class ErrorHelper
    {
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static readonly int baseBuilderErrorCode = -60000;

        internal static AgentErrorDefinition InvalidAccessTokenForAgentCallback = new AgentErrorDefinition(-60000, Properties.Resources.ConversationResponseUnAuthorized, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition SendGetConversationsError = new AgentErrorDefinition(-60001, Properties.Resources.GetConversationsError, "https://aka.ms/AgentsSDK-Error02");
        internal static AgentErrorDefinition SendCreateConversationError = new AgentErrorDefinition(-60002, Properties.Resources.CreateConversationError, "https://aka.ms/AgentsSDK-Error03");
        internal static AgentErrorDefinition SendSendConversationError = new AgentErrorDefinition(-60003, Properties.Resources.SendToConversationError, "https://aka.ms/AgentsSDK-Error04");
        internal static AgentErrorDefinition SendConversationHistoryError = new AgentErrorDefinition(-60004, Properties.Resources.SendConversationHistoryError, "https://aka.ms/AgentsSDK-Error05");
        internal static AgentErrorDefinition SendUpdateActivityError = new AgentErrorDefinition(-60005, Properties.Resources.SendUpdateActivityError, "https://aka.ms/AgentsSDK-Error06");
        internal static AgentErrorDefinition SendReplyToActivityError = new AgentErrorDefinition(-60006, Properties.Resources.ReplyToActivityError, "https://aka.ms/AgentsSDK-Error07");
        internal static AgentErrorDefinition SendDeleteActivityError = new AgentErrorDefinition(-60007, Properties.Resources.SendDeleteActivity, "https://aka.ms/AgentsSDK-Error08");
        internal static AgentErrorDefinition SendGetConversationMembersError = new AgentErrorDefinition(-60008, Properties.Resources.SendGetConversationMembers, "https://aka.ms/AgentsSDK-Error09");
        internal static AgentErrorDefinition SendGetConversationMemberError = new AgentErrorDefinition(-60009, Properties.Resources.SendGetConversationMember, "https://aka.ms/AgentsSDK-Error10");
        internal static AgentErrorDefinition SendDeleteConversationMemberError = new AgentErrorDefinition(-60010, Properties.Resources.SendDeleteConversationMember, "https://aka.ms/AgentsSDK-Error11");
        internal static AgentErrorDefinition SendGetConversationPagedMembersError = new AgentErrorDefinition(-60011, Properties.Resources.SendGetConversationPagedMembers, "https://aka.ms/AgentsSDK-Error12");
        internal static AgentErrorDefinition SendGetActivityMembersError = new AgentErrorDefinition(-60012, Properties.Resources.SendGetActivityMembers, "https://aka.ms/AgentsSDK-Error13");
        internal static AgentErrorDefinition SendUploadAttachmentError = new AgentErrorDefinition(-60013, Properties.Resources.SendUploadAttachment, "https://aka.ms/AgentsSDK-Error14");
        internal static AgentErrorDefinition GetSignInResourceAsync_BadRequestError = new AgentErrorDefinition(-60014, Properties.Resources.GetSignInResourceAsync_BadRequest, "https://aka.ms/AgentsSDK-Error15");
        internal static AgentErrorDefinition GetAttachmentError = new AgentErrorDefinition(-60015, Properties.Resources.GetAttachment_Error, "https://aka.ms/AgentsSDK-Error16");
        internal static AgentErrorDefinition GetAttachmentInfoError = new AgentErrorDefinition(-60016, Properties.Resources.GetAttachmentInfoError, "https://aka.ms/AgentsSDK-Error17");
        internal static AgentErrorDefinition TokenServiceExchangeFailed = new AgentErrorDefinition(-60017, Properties.Resources.TokenServiceExchangeFailed, "https://aka.ms/AgentsSDK-Error18");
        internal static AgentErrorDefinition TokenServiceExchangeUnexpected = new AgentErrorDefinition(-60018, Properties.Resources.TokenServiceExchangeUnexpected, "https://aka.ms/AgentsSDK-Error19");
        internal static AgentErrorDefinition TokenServiceGetTokenUnexpected = new AgentErrorDefinition(-60018, Properties.Resources.TokenServiceGetTokenUnexpected, "https://aka.ms/AgentsSDK-Error19");
        internal static AgentErrorDefinition TokenServiceGetAadTokenUnexpected = new AgentErrorDefinition(-60019, Properties.Resources.TokenServiceGetAadTokenUnexpected, "https://aka.ms/AgentsSDK-Error20");
        internal static AgentErrorDefinition TokenServiceSignOutUnexpected = new AgentErrorDefinition(-60020, Properties.Resources.TokenServiceSignOutUnexpected, "https://aka.ms/AgentsSDK-Error21");
        internal static AgentErrorDefinition TokenServiceGetTokenStatusUnexpected = new AgentErrorDefinition(-60021, Properties.Resources.TokenServiceGetTokenStatusUnexpected, "https://aka.ms/AgentsSDK-Error22");
        internal static AgentErrorDefinition TokenServiceGetTokenOrSignInResourceUnexpected = new AgentErrorDefinition(-60022, Properties.Resources.TokenServiceGetTokenOrSignInResourceUnexpected, "https://aka.ms/AgentsSDK-Error23");
        internal static AgentErrorDefinition TokenServiceGetSignInUrlUnexpected = new AgentErrorDefinition(-60023, Properties.Resources.TokenServiceGetSignInUrlUnexpected, "https://aka.ms/AgentsSDK-Error24");
        internal static AgentErrorDefinition TokenServiceGetSignInResourceUnexpected = new AgentErrorDefinition(-60024, Properties.Resources.TokenServiceGetSignInResourceUnexpected, "https://aka.ms/AgentsSDK-Error25");
        internal static AgentErrorDefinition TokenServiceExchangeErrorResponse = new AgentErrorDefinition(-60025, Properties.Resources.TokenServiceExchangeErrorResponse, "https://aka.ms/AgentsSDK-Error26");
    }
}
