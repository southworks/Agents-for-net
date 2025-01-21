// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.15.2

using System.Text.Json;
using Microsoft.Agents.BotBuilder.Teams;
using Microsoft.Agents.Core.Interfaces;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Teams.Models;

namespace MeetingContext.Bots
{
    public class MeetingContextBot : TeamsActivityHandler
    {
        public const string commandString = "Please use one of these two commands: **Meeting Context** or **Participant Context**";
        
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text != null)
            {
                var text = turnContext.Activity.RemoveRecipientMention();
                if (text.ToLower().Contains("participant context"))
                {
                    var channelDataObject = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(turnContext.Activity.ChannelData));

                    var tenantId = channelDataObject.GetProperty("tenant").GetProperty("id").GetString();
                    var meetingId = channelDataObject.GetProperty("meeting").GetProperty("id").GetString();
                    var participantId = turnContext.Activity.From.AadObjectId;

                    // GetMeetingParticipant
                    TeamsMeetingParticipant participantDetails = await TeamsInfo.GetMeetingParticipantAsync(turnContext, meetingId, participantId, tenantId).ConfigureAwait(false);

                    var formattedString = this.GetFormattedSerializeObject(participantDetails);

                    await turnContext.SendActivityAsync(MessageFactory.Text(formattedString), cancellationToken);
                }
                else if (text.ToLower().Contains("meeting context"))
                {
                    MeetingInfo meetingInfo = await TeamsInfo.GetMeetingInfoAsync(turnContext);

                    var formattedString = this.GetFormattedSerializeObject(meetingInfo);

                    await turnContext.SendActivityAsync(MessageFactory.Text(formattedString), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(commandString), cancellationToken);
                }
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and Welcome!";

            await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text(commandString), cancellationToken);
        }

        /// <summary>
        /// Gets the serialize formatted object string.
        /// </summary>
        /// <param name="obj">Incoming object needs to be formatted.</param>
        /// <returns>Formatted string.</returns>
        private string GetFormattedSerializeObject (object obj)
        {
            var formattedString = "";
            foreach (var meetingDetails in obj.GetType().GetProperties())
            {
                var detail = meetingDetails.GetValue(obj, null);
                var block = $"<b>{meetingDetails.Name}:</b> <br>";
                var storeTemporaryFormattedString = "";

                if (detail != null)
                {
                    if (detail.GetType().Name != "String")
                    {
                        foreach (var value in detail.GetType().GetProperties())
                        {
                            storeTemporaryFormattedString += $" <b> &nbsp;&nbsp;{value.Name}:</b> {value.GetValue(detail, null)}<br/>";
                        }

                        Console.WriteLine(storeTemporaryFormattedString);

                        formattedString += block + storeTemporaryFormattedString;
                        storeTemporaryFormattedString = String.Empty;
                    }
                }
            }

            return formattedString;
        }
    }
}
