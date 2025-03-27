// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models
{
    public class TokenOrSignInResourceResponse
    {
        public TokenOrSignInResourceResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenOrSignInResourceResponse"/> class.
        /// </summary>
        /// <param name="tokenResponse">Token response.</param>
        /// <param name="signInResource">SignIn resource.</param>
        public TokenOrSignInResourceResponse(TokenResponse tokenResponse = default, SignInResource signInResource = default)
        {
            TokenResponse = tokenResponse;
            SignInResource = signInResource;
        }

        /// <summary>
        /// Gets or sets the TokenResponse.
        /// </summary>
        public TokenResponse TokenResponse { get; set; }

        /// <summary>
        /// Gets or sets the SignInResource.
        /// </summary>
        public SignInResource SignInResource { get; set; }

        /// <summary>
        /// Gets properties that are not otherwise defined by the <see cref="Activity"/> type but that
        /// might appear in the serialized REST JSON object.
        /// </summary>
        /// <value>The extended properties for the object.</value>
        /// <remarks>With this, properties not represented in the defined type are not dropped when
        /// the JSON object is deserialized, but are instead stored in this property. Such properties
        /// will be written to a JSON object when the instance is serialized.</remarks>
        public IDictionary<string, JsonElement> Properties { get; set; } = new Dictionary<string, JsonElement>();
    }
}
