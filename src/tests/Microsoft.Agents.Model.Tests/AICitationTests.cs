// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class AICitationTests
    {
        [Fact]
        public void AIEntity_Roundtrip()
        {
            // Arrange
            var entityOut = new AIEntity()
            {
                AtType = "atType",
                AtContext = "atContext",
                AtId = "atId",
                Citation = [
                    new ClientCitation()
                    {
                        Position = 1,
                        Appearance = new ClientCitationAppearance()
                        {
                            Name = "name",
                            Text = "text",
                            Url = "url",
                            Image = new AppearanceImage()
                            {
                                Name = "image",
                            }
                        }
                    }
                ]
            };

            var expected = "{\"citation\":[{\"position\":1,\"appearance\":{\"name\":\"name\",\"text\":\"text\",\"url\":\"url\",\"abstract\":\"\",\"image\":{\"type\":\"ImageObject\",\"name\":\"image\"},\"@type\":\"DigitalDocument\"},\"@type\":\"Claim\"}],\"type\":\"https://schema.org/Message\",\"@type\":\"atType\",\"@context\":\"atContext\",\"@id\":\"atId\",\"additionalType\":[\"AIGeneratedContent\"],\"properties\":{}}";

            // Test serialize
            var jsonOut = ProtocolJsonSerializer.ToJson(entityOut);
            Assert.Equal(expected, jsonOut);

            // Test deserialize
            var entityIn = ProtocolJsonSerializer.ToObject<AIEntity>(jsonOut);
            var deserializeJson = ProtocolJsonSerializer.ToJson(entityIn);
            Assert.Equal(expected, deserializeJson);
        }
    }
}
