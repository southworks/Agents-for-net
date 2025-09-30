using Microsoft.Agents.Core.Models;


namespace Microsoft.Kairo.Sdk.AgentsSdkExtensions.Models
{
    public class EmailReference : Entity
    {
        // Need this because the Entity Type name is different then the class name. 
        public static readonly string EntityTypeName = "emailNotification";

        public EmailReference() : base(EntityTypeName)
        {

        }

        public string? Id { get; set; }
        public string? ConversationId { get; set; }
        public string? HtmlBody { get; set; }
    }
}
