// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models
{
    public class StreamInfo : Entity
    {
        public StreamInfo() : base(EntityTypes.StreamInfo)
        {
        }

        public string StreamId { get; set; }
        public StreamType StreamType { get; set; }
        public int StreamSequence { get; set; }
        public StreamResult StreamResult { get; set; }
    }
}
