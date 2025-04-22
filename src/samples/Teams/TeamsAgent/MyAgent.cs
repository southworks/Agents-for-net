using AdaptiveCards.Rendering;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.AdaptiveCards;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.App;
using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Agents.Extensions.Teams.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TeamsAgent
{
    public class MyAgent : AgentApplication
    {
        public MyAgent(AgentApplicationOptions options) : base(options)
        {
            RegisterExtension(new TeamsAgentExtension(this), tae =>
            {
                tae.OnMessageEdit(MessageEdited);
                tae.MessageExtensions.OnSelectItem(OnSelectItem);
                tae.MessageExtensions.OnQuery("findNuGetPackage", OnQuery);
            });
            OnMessageReactionsAdded(OnMessageReaction);
            OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
            OnActivity(ActivityTypes.Message, OnMessageAsync);
        }

        private async Task<MessagingExtensionResult> OnSelectItem(ITurnContext turnContext, ITurnState turnState, object item, CancellationToken cancellationToken)
        {
            string jsonItem = JsonSerializer.Serialize(item);
            await turnContext.SendActivityAsync("select item " + jsonItem, cancellationToken: cancellationToken);
            return new MessagingExtensionResult() { ActivityPreview = new Activity() { Type = ActivityTypes.Message, Text = jsonItem } };
        }

        private async Task<MessagingExtensionResult> OnQuery(ITurnContext turnContext, ITurnState turnState, Query<IDictionary<string, object>> query, CancellationToken cancellationToken)
        {

            JsonElement el = query.Parameters.TryGetValue<JsonElement>("NuGetPackageName");

            string text = el.GetString() ?? string.Empty;


            IEnumerable<PackageItem> packages = await FindPackages(text);
            List<MessagingExtensionAttachment> attachments = [.. packages.Select(package =>
            {
                string cardValue = $"{{\"packageId\": \"{package.Id}\", \"version\": \"{package.Version}\", \"description\": \"{PackageItem.NormalizeString(package.Description!)}\", \"projectUrl\": \"{package.ProjectUrl}\", \"iconUrl\": \"{package.IconUrl}\"}}";

                ThumbnailCard previewCard = new() { Title = package.Id, Tap = new CardAction { Type = "invoke", Value = cardValue } };
                if (!string.IsNullOrEmpty(package.IconUrl))
                {
                    previewCard.Images = [new CardImage(package.IconUrl, "Icon")];
                }

                MessagingExtensionAttachment attachment = new()
                {
                    ContentType = HeroCard.ContentType,
                    Content = new HeroCard { Title = package.Id },
                    Preview = previewCard.ToAttachment()
                };

                return attachment;
            })];

            return new MessagingExtensionResult
            {
                Type = "result",
                AttachmentLayout = "list",
                Attachments = attachments

            };
        }

        private static async Task<IEnumerable<PackageItem>> FindPackages(string text)
        {
            JsonNode? obj = JsonObject.Parse(await new HttpClient().GetStringAsync($"https://azuresearch-usnc.nuget.org/query?q=id:{text}&prerelease=true"));
            List<JsonObject> items = ProtocolJsonSerializer.ToObject<List<JsonObject>>(obj?["data"]!);
            return items.Select(item => new PackageItem()
            {
                Id = item["id"]?.ToString(),
                Version = item["version"]?.ToString(),
                Description = item["description"]?.ToString(),
                ProjectUrl = item["projectUrl"]?.ToString(),
                IconUrl = item["iconUrl"]?.ToString()
            });
        }

        private async Task OnMessageReaction(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync("Message Reaction" + turnContext.Activity.ReactionsAdded[0].Type, cancellationToken: cancellationToken);
        }

        private async Task MessageEdited(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync("Message Edited", cancellationToken: cancellationToken);
        }

        private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
                }
            }
        }

        private async Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
        {
            TeamsChannelAccount member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
            await turnContext.SendActivityAsync(member.Email, cancellationToken: cancellationToken);

        }
    }

    class PackageItem
    {
        public string? Id;
        public string? Version;
        public string? Description;
        public string? ProjectUrl;
        public string? IconUrl;

        public static string NormalizeString(string value)
        {
            return value
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("\"", "\\\"");
        }
    }
}
