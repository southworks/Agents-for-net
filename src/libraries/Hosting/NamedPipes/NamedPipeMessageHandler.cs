// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.NamedPipes.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.NamedPipes
{
    /// <summary>
    /// Custom <see cref="HttpMessageHandler"/> that intercepts outgoing HTTP requests destined for
    /// the named pipe service URL (urn:botframework:namedpipe:*) and routes them
    /// back through the named pipe protocol instead of making an actual HTTP call.
    /// </summary>
    /// <remarks>
    /// This enables the Agents SDK's ConversationsRestClient to send reply activities
    /// back to DirectLineFlex through the named pipe, without any HTTP roundtrip.
    /// </remarks>
    public sealed class NamedPipeMessageHandler : HttpMessageHandler
    {
        private readonly ILogger _logger;
        private NamedPipeProtocol _protocol;

        /// <summary>
        /// Initializes a new instance of the <see cref="NamedPipeMessageHandler"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public NamedPipeMessageHandler(ILogger<NamedPipeMessageHandler> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Set the active named pipe protocol instance. Called by the hosted service
        /// when a pipe connection is established.
        /// </summary>
        /// <param name="protocol">The protocol instance, or null when disconnected.</param>
        public void SetProtocol(NamedPipeProtocol protocol)
        {
            _protocol = protocol;
        }

        /// <summary>
        /// Send a request through the named pipe. Invoked by <see cref="PipeRoutingDelegatingHandler"/>.
        /// </summary>
        /// <param name="request">The HTTP request to route.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An HTTP response constructed from the pipe response.</returns>
        public Task<HttpResponseMessage> SendViaPipeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => SendAsync(request, cancellationToken);

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri == null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (!uri.Scheme.Equals("urn", StringComparison.OrdinalIgnoreCase)
                && !uri.AbsoluteUri.Contains("botframework:namedpipe", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("NamedPipeMessageHandler: Unexpected non-pipe URL: {Url}.", uri);
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            if (_protocol == null)
            {
                _logger.LogError("NamedPipeMessageHandler: No active pipe connection for outbound request to {Path}.", uri.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            // Extract the /v3/... path from the urn: URI
            var fullUri = uri.AbsoluteUri;
            var pathStart = fullUri.IndexOf("/v3/", StringComparison.OrdinalIgnoreCase);
            var path = pathStart >= 0 ? fullUri[pathStart..] : uri.AbsolutePath;

            var verb = request.Method.Method;
            byte[] body = null;
            if (request.Content != null)
            {
                body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogDebug("NamedPipeMessageHandler: Routing {Verb} {Path} through pipe (BodyLen={Len}).",
                verb, path, body?.Length ?? 0);

            try
            {
                var response = await _protocol.SendRequestAsync(verb, path, body, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("NamedPipeMessageHandler: Pipe response {StatusCode} for {Path}.",
                    response.StatusCode, path);

                var httpResponse = new HttpResponseMessage((HttpStatusCode)response.StatusCode);
                if (response.Body != null && response.Body.Length > 0)
                {
                    httpResponse.Content = new ByteArrayContent(response.Body);
                    httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }

                return httpResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeMessageHandler: Failed to send through pipe for {Path}.", path);
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }
        }
    }
}
