using Microsoft.Agents.Core.Models;

namespace Microsoft.Kairo.Sdk.AgentsSdkExtensions.Models
{
    public class WpxComment : Entity
    {
        public string? OdataId { get; set; }
        public string? DocumentId { get; set; }
        public string? InitiatingCommentId { get; set; }
        public string? SubjectCommentId { get; set; }
    }
}
