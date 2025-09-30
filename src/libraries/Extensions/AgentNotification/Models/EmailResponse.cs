using Microsoft.Agents.Core.Models;

namespace AgentNotification.Models
{
    public class EmailResponse : Entity
    {
        /// <summary>
        /// HTML Body of the email Response.
        /// </summary>
        public string? HtmlBody { get; set; } = string.Empty;
        public string? Id { get; set; } = string.Empty;
        public string? ConversationId { get; set; } = string.Empty;

        public EmailResponse(string? emailHtmlBody = default) : base("emailResponse")
        {
            HtmlBody = emailHtmlBody;
        }
    }

}
