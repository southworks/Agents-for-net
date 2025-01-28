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
        private readonly Mock<IOptions<MsalAuthConfigurationOptions>> _options;
        private readonly Mock<IServiceProvider> _service = new Mock<IServiceProvider>();
        private readonly MsalAuthConfigurationOptions _returnedOptions = new MsalAuthConfigurationOptions
        {
            MSALRetryCount = 4
        };

        private readonly Mock<HttpMessageHandler> _handler = new Mock<HttpMessageHandler>();

        private const string RequestUri = "http://test.com";

        public MSALHttpRetryHandlerHelperTests()
        {
            _options = new Mock<IOptions<MsalAuthConfigurationOptions>>();
            _options.Setup(x => x.Value).Returns(_returnedOptions);
            
            _service.Setup(x => x.GetService(typeof(IOptions<MsalAuthConfigurationOptions>))).Returns(_options.Object);
        }

        [Fact]
        public void Constructor_ShouldInstantiateCorrectly()
        {
            var retryHelper = new MSALHttpRetryHandlerHelper(_service.Object);

            Assert.NotNull(retryHelper);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponse()
        {
            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnSuccessfulResponseAfterRetries()
        {
            _handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            _handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseOnNonRetryableFailure()
        {
            _handler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _handler.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task SendAsync_ShouldReturnResponseAfterExhaustsAllRetries()
        {
            _handler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout));

            var retryHandler = new MSALHttpRetryHandlerHelper(_service.Object)
            {
                InnerHandler = _handler.Object
            };

            var httpClient = new HttpClient(retryHandler);

            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, RequestUri));

            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
            _handler.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }
    }
}
