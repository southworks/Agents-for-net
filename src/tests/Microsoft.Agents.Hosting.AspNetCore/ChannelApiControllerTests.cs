// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.Tests
{
    public class ChannelApiControllerTests
    {
        [Fact]
        public async Task SendToConversationAsync_ShouldCallHandlerWithActivity()
        {
            var conversationId = Guid.NewGuid().ToString();
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnSendToConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            var activity = new Activity { Text = "test" };
            AddControllerContext(controller, activity);

            var result = await controller.SendToConversationAsync(conversationId) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task SendToConversationAsync_ShouldNotCallHandlerWithoutActivity()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnSendToConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.SendToConversationAsync("");

            Assert.Null(result);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldCallHandlerWithActivity()
        {
            var conversationId = Guid.NewGuid().ToString();
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnReplyToActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            var activity = new Activity { Id = "123", Text = "test" };
            AddControllerContext(controller, activity);

            var result = await controller.ReplyToActivityAsync(conversationId, activity.Id) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task ReplyToActivityAsync_ShouldNotCallHandlerWithoutActivity()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnReplyToActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.ReplyToActivityAsync("", "");

            Assert.Null(result);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldCallHandlerWithActivity()
        {
            var conversationId = Guid.NewGuid().ToString();
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnUpdateActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            var activity = new Activity { Id = "123", Text = "test" };
            AddControllerContext(controller, activity);

            var result = await controller.UpdateActivityAsync(conversationId, activity.Id) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task UpdateActivityAsync_ShouldNotCallHandlerWithoutActivity()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnUpdateActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Activity>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Never);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.UpdateActivityAsync("", "");

            Assert.Null(result);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task DeleteActivityAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnDeleteActivityAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            await controller.DeleteActivityAsync("", "");

            Mock.Verify(handler);
        }

        [Fact]
        public async Task GetActivityMembersAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnGetActivityMembersAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new()])
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.GetActivityMembersAsync("", "") as JsonResult;

            Assert.Single(result.Value as List<ChannelAccount>);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task CreateConversationAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            var resource = new Connector.Types.ConversationResourceResponse { Id = "test" };
            handler.Setup(e => e.OnCreateConversationAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<ConversationParameters>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(resource)
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.CreateConversationAsync(new()) as JsonResult;

            Assert.Equal(resource.Id, (result.Value as Connector.Types.ConversationResourceResponse).Id);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task GetConversationsAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            var conversationResult = new Connector.Types.ConversationsResult { ContinuationToken = "token" };
            handler.Setup(e => e.OnGetConversationsAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(conversationResult)
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.GetConversationsAsync(conversationResult.ContinuationToken) as JsonResult;

            Assert.Equal(conversationResult.ContinuationToken, (result.Value as Connector.Types.ConversationsResult).ContinuationToken);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task GetConversationMembersAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnGetConversationMembersAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([new()])
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.GetConversationMembersAsync("") as JsonResult;

            Assert.Single(result.Value as List<ChannelAccount>);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task GetConversationMemberAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            var channelAccount = new ChannelAccount { Id = "test" };
            handler.Setup(e => e.OnGetConversationMemberAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(channelAccount)
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.GetConversationMemberAsync("", "") as JsonResult;

            Assert.Equal(channelAccount.Id, (result.Value as ChannelAccount).Id);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task GetConversationPagedMembersAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            var pagedMembersResult = new PagedMembersResult { ContinuationToken = "token" };
            handler.Setup(e => e.OnGetConversationPagedMembersAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<int?>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(pagedMembersResult)
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.GetConversationPagedMembersAsync("") as JsonResult;

            Assert.Equal(pagedMembersResult.ContinuationToken, (result.Value as PagedMembersResult).ContinuationToken);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task DeleteConversationMemberAsync_ShouldCallHandler()
        {
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnDeleteConversationMemberAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            await controller.DeleteConversationMemberAsync("", "");

            Mock.Verify(handler);
        }

        [Fact]
        public async Task SendConversationHistoryAsync_ShouldCallHandler()
        {
            var conversationId = Guid.NewGuid().ToString();
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnSendConversationHistoryAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<Transcript>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.SendConversationHistoryAsync(conversationId, null) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            Mock.Verify(handler);
        }

        [Fact]
        public async Task UploadAttachmentAsync_ShouldCallHandler()
        {
            var conversationId = Guid.NewGuid().ToString();
            var handler = new Mock<IChannelApiHandler>();
            handler.Setup(e => e.OnUploadAttachmentAsync(
                    It.IsAny<ClaimsIdentity>(),
                    It.IsAny<string>(),
                    It.IsAny<AttachmentData>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ResourceResponse(conversationId))
                .Verifiable(Times.Once);
            var controller = new ChannelApiController(handler.Object);
            AddControllerContext(controller);

            var result = await controller.UploadAttachmentAsync(conversationId, null) as JsonResult;

            Assert.Equal(conversationId, (result.Value as ResourceResponse).Id);
            Mock.Verify(handler);
        }

        // TODO: create a helper class to reuse duplicated code.

        private void AddControllerContext(ChannelApiController controller, Activity activity = null)
        {
            var content = activity == null ? "" : JsonConvert.SerializeObject(activity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(),
                    Request = { Body = new MemoryStream(Encoding.UTF8.GetBytes(content)) }
                }
            };

        }
    }
}