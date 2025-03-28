// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Agents.Connector.HeaderPropagation;

/// <summary>
/// Shared context between the <see cref="HeaderPropagationMiddleware"/> and <see cref="AgentsHttpClientFactory"/> for propagating headers.
/// </summary>
public class HeaderPropagationContext()
{
    private static readonly AsyncLocal<IHeaderDictionary> _headers = new();
    private static readonly IHeaderDictionary _headersToPropagate;

    static HeaderPropagationContext()
    {
        _headersToPropagate = HeaderPropagationAttribute.SetHeadersSerialization();
    }

    /// <summary>
    /// Gets or sets the headers allowed by the <see cref="HeaderPropagationOptions"/> class to be propagated when the <see cref="HttpClient"/> gets created.
    /// </summary>
    public IHeaderDictionary Headers
    {
        get
        {
            return _headers.Value;
        }
        set
        {
            _headers.Value = FilterHeaders(value);
        }
    }

    /// <summary>
    /// Filters the request headers based on the keys provided in <see cref="HeaderPropagationOptions"/>.
    /// </summary>
    /// <param name="requestHeaders">Headers collection from an Http request.</param>
    /// <returns>Filtered headers.</returns>
    private HeaderDictionary FilterHeaders(IHeaderDictionary requestHeaders)
    {
        var result = new HeaderDictionary();

        // Create a copy to ensure headers are not modified by the original request.
        var headers = requestHeaders.ToDictionary(StringComparer.InvariantCultureIgnoreCase);

        if (requestHeaders == null || requestHeaders.Count == 0)
        {
            return result;
        }
        
        headers.TryGetValue("x-ms-correlation-id", out var correlationId);

        result.TryAdd("x-ms-correlation-id", correlationId);

        foreach (var item in _headersToPropagate)
        {
            if (headers.TryGetValue(item.Key, out var header))
            {
                result.TryAdd(item.Key, header);
            }
        }

        return result;
    }
}