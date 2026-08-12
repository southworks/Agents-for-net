// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams.Models;
using Microsoft.Teams.Apps.Schema;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Extensions.MSTeams.Tests
{
    public class TeamsActivityExtensionsTests
    {
        [Fact]
        public void TeamsGetSelectedChannelId_ShouldReturnChannelId()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { settings = new { selectedChannel = new { id = "channel123" } } }) };

            var channelId = activity.TeamsGetSelectedChannelId();

            Assert.Equal("channel123", channelId);
        }

        [Fact]
        public void TeamsGetSelectedChannelId_ShouldReturnNullOnNullSettings()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { }) };

            var channelId = activity.TeamsGetSelectedChannelId();

            Assert.Null(channelId);
        }

        [Fact]
        public void TeamsGetMeetingInfo_ShouldReturnMeetingId()
        {
            var activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { meeting = new { id = "meeting123" } }) };

            var meetingId = activity.TeamsGetMeetingInfo().Id;

            Assert.Equal("meeting123", meetingId);
        }

        [Fact]
        public void TeamsGetChannelId_ShouldReturnChannelId()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { channel = new { id = "channel123" } }) };

            var channelId = activity.TeamsGetChannelId();

            Assert.Equal("channel123", channelId);
        }

        [Fact]
        public void TeamsGetChannelId_ShouldReturnNullOnNullChannel()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { }) };

            var channelId = activity.TeamsGetChannelId();

            Assert.Null(channelId);
        }

        [Fact]
        public void TeamsGetTeamInfo_ShouldReturnTeamId()
        {
            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { team = new { id = "team1234" } }) };

            var teamId = activity.TeamsGetTeamInfo().Id;

            Assert.Equal("team1234", teamId);
        }

        [Fact]
        public void TeamsGetTeamInfo_ShouldReturnTeamIdFromTypedActivity()
        {
            IMessageActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { team = new { id = "team123" } }) };

            var teamId = activity.TeamsGetTeamInfo().Id;

            Assert.Equal("team123", teamId);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldConfigureAlert()
        {
            var activity = new Activity { };

            activity.TeamsNotifyUser();

            var notification = GetNotification(activity);
            Assert.Equal(true, notification.Alert);
            Assert.Equal(false, notification.AlertInMeeting);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldConfigureAlertInMeeting()
        {
            var activity = new Activity { };

            activity.TeamsNotifyUser(alertInMeeting: true);

            var notification = GetNotification(activity);
            Assert.Equal(true, notification.AlertInMeeting);
            Assert.Equal(false, notification.Alert);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldUseExternalResourceUrl()
        {
            string resourceUrl = "https://microsoft.com";

            var activity = new Activity { };

            activity.TeamsNotifyUser(false, externalResourceUrl: resourceUrl);

            Assert.Equal(resourceUrl, GetNotification(activity).ExternalResourceUrl);
        }

        [Fact]
        public void TeamsNotifyUser_ShouldNotOverrideExistingChannelData()
        {
            var activity = new Activity { ChannelData = new TeamsChannelData { Team = new Team { Id = "team123" } } };

            activity.TeamsNotifyUser();

            Assert.True(GetNotification(activity).Alert);
            Assert.Equal("team123", ((TeamsChannelData)activity.ChannelData).Team.Id);
        }

        [Fact]
        public void TeamsGetTeamOnBehalfOf_ShouldReturnOnBehalfOf()
        {
            var onBehalfOf = new TeamsOnBehalfOf
            {
                DisplayName = "TestOnBehalfOf",
                ItemId = 0,
                MentionType = "person",
                Mri = Guid.NewGuid().ToString()
            };

            IActivity activity = new Activity { ChannelData = JsonSerializer.SerializeToElement(new { onBehalfOf = new List<TeamsOnBehalfOf> { onBehalfOf } }) };

            var onBehalfOfList = activity.TeamsGetTeamOnBehalfOf();

            Assert.Single(onBehalfOfList);
            Assert.Equal("TestOnBehalfOf", onBehalfOfList[0].DisplayName);
        }

        [Fact]
        public void TeamsEnableFeedbackLoop_ShouldAddFeedbackLoopData()
        {
            var activity = new Activity();

            var result = activity.TeamsEnableFeedbackLoop("custom");

            Assert.True(result);
            var channelData = JsonSerializer.SerializeToElement(activity.ChannelData);
            Assert.Equal("custom", channelData.GetProperty("feedbackLoop").GetProperty("type").GetString());
        }

        [Fact]
        public void TeamsEnableFeedbackLoop_ShouldReturnFalse_WhenChannelDataAlreadySet()
        {
            var existingChannelData = new TeamsChannelData { Team = new Team { Id = "team123" } };
            var activity = new Activity { ChannelData = existingChannelData };

            var result = activity.TeamsEnableFeedbackLoop();

            Assert.False(result);
            Assert.Same(existingChannelData, activity.ChannelData);
        }

        private static TeamsNotification GetNotification(IActivity activity)
        {
            var channelData = Assert.IsType<TeamsChannelData>(activity.ChannelData);
            return ProtocolJsonSerializer.ToObject<TeamsNotification>(channelData.Properties["notification"]);
        }
    }
}
