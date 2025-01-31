// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.SharePoint.Serialization.Converters;
using System.Text.Json;

namespace Microsoft.Agents.SharePoint.Serialization
{
    public static class SerializerExtensions
    {
        public static JsonSerializerOptions ApplySharePointOptions(this JsonSerializerOptions options)
        {
            options.Converters.Add(new AceDataConverter());
            options.Converters.Add(new AceRequestConverter());

            return options;
        }
    }
}
