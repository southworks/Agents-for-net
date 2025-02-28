﻿using Microsoft.Agents.MCP.Core.Abstractions;
using Microsoft.Agents.MCP.Core.Handlers.Contracts.ServerMethods.Initialize;
using Microsoft.Agents.MCP.Core.Payloads;
using System.Text.Json;

namespace Microsoft.Agents.MCP.Client.Handlers.Initialization
{
    public static class ClientRequestHelpers
    {
        public static async Task InitializeAsync(
          IMcpSession session,
          InitializationParameters parameters,
          CancellationToken ct)
        {
            var result = await SendAsync<InitializationResult>(session, new McpInitializeRequest(parameters), ct);
            // TODO: Validate server response result
            await SendAsync(session, new McpInitializeNotification(InitializeNotificationParameters.Instance), ct);
        }

        public static async Task SendAsync(
           IMcpSession session,
           McpNotification request,
           CancellationToken ct)
        {
            await session.WriteOutgoingPayload(request, ct);
        }

        public static async Task<ResponseType?> SendAsync<ResponseType>(
            IMcpSession session,
            McpRequest request, 
            CancellationToken ct)
        {
            var results = session.GetIncomingSessionStream(ct);
            await session.WriteOutgoingPayload(request, ct);
            await foreach(var item in results)
            {
                if (item is McpResult<JsonElement> response && response.Id == request.Id)
                {
                    // Try Parse JsonElement as InitializeResponse
                    var result = JsonSerializer.Deserialize<ResponseType>(response.TypedResult.GetRawText());
                    return result;
                }
                else if (item is McpError errorResponse && errorResponse.Id == request.Id)
                {
                    throw new InvalidOperationException($"Encountered error during call. Code: {errorResponse.Error?.Code}, Message: {errorResponse.Error?.Message}");
                }
            }

            throw new InvalidOperationException($"Connection Closed");
        }
    }
}
