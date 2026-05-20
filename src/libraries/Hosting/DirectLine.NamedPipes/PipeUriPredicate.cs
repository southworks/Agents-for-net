// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Helpers for identifying request URIs that must be routed through the named pipe transport.
    /// </summary>
    internal static class PipeUriPredicate
    {
        /// <summary>
        /// The required scheme + authority prefix produced by the named pipe ingress.
        /// </summary>
        /// <remarks>
        /// Matches the service URL convention <c>urn:botframework:namedpipe:*</c>. The colon
        /// after the keyword is included so unrelated URNs such as <c>urn:botframework:namedpipes-other</c>
        /// or query strings that merely contain the substring are not misclassified.
        /// </remarks>
        public const string NamedPipeUriPrefix = "urn:botframework:namedpipe:";

        /// <summary>
        /// Returns true when the URI is a well-formed named-pipe service URL that should be
        /// routed through the local pipe transport rather than over HTTP.
        /// </summary>
        /// <param name="uri">The URI to evaluate. May be null.</param>
        public static bool IsNamedPipeUri(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }

            return string.Equals(uri.Scheme, "urn", StringComparison.OrdinalIgnoreCase)
                && uri.AbsoluteUri.StartsWith(NamedPipeUriPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
