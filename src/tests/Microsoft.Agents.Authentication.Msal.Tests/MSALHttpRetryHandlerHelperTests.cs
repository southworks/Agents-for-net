// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests
{
    public class MSALHttpRetryHandlerHelperTests
    {
        private readonly Mock<IOptions<MsalAuthConfigurationOptions>> Options;
        private readonly Mock<IServiceProvider> Service = new Mock<IServiceProvider>();
        private readonly MsalAuthConfigurationOptions ReturnedOptions = new MsalAuthConfigurationOptions
        {
            MSALRetryCount = 4
        };

        private readonly Mock<HttpMessageHandler> Handler = new Mock<HttpMessageHandler>();

        public MSALHttpRetryHandlerHelperTests()
        {
            Options = new Mock<IOptions<MsalAuthConfigurationOptions>>();
            Options.Setup(x => x.Value).Returns(ReturnedOptions);
            
            Service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(Options.Object);
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var retryHelper = new MSALHttpRetryHandlerHelper(Service.Object);

            Assert.NotNull(retryHelper);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponse()
        {
            Handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var retryHandler = new MSALHttpRetryHandlerHelper(Service.Object)
            {
                InnerHandler = Handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponseAfterRetries()
        {
            Handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var retryHandler = new MSALHttpRetryHandlerHelper(Service.Object)
            {
                InnerHandler = Handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseOnNonRetryableFailure()
        {
            Handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

            var retryHandler = new MSALHttpRetryHandlerHelper(Service.Object)
            {
                InnerHandler = Handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseAfterExhaustsAllRetries()
        {
            Handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout));

            var retryHandler = new MSALHttpRetryHandlerHelper(Service.Object)
            {
                InnerHandler = Handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"));

            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
            Handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }
    }
}
