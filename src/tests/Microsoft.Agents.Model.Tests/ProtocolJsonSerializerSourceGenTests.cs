// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ProtocolJsonSerializerSourceGenTests
    {
        [Fact]
        public void AddTypeInfoResolver_SetsNonNullResolver()
        {
            // Arrange
            var resolver = new DefaultJsonTypeInfoResolver();

            // Act
            ProtocolJsonSerializer.AddTypeInfoResolver(resolver);

            // Assert
            Assert.NotNull(ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver);
        }

        [Fact]
        public void AddTypeInfoResolver_NewResolverConsultedFirst()
        {
            // Arrange: a resolver that handles HeroCard and tracks if it was called
            var wasCalled = false;
            var trackingResolver = new TrackingTypeInfoResolver(
                typeof(Microsoft.Agents.Core.Models.HeroCard),
                () => wasCalled = true);

            // Act
            ProtocolJsonSerializer.AddTypeInfoResolver(trackingResolver);
            var json = """{"title":"Test"}""";
            JsonSerializer.Deserialize<Microsoft.Agents.Core.Models.HeroCard>(
                json, ProtocolJsonSerializer.SerializationOptions);

            // Assert
            Assert.True(wasCalled, "The most-recently registered resolver should be consulted first");
        }

        [Fact]
        public async Task AddTypeInfoResolver_ConcurrentCalls_DoNotThrow()
        {
            // Act: 20 concurrent calls with harmless resolvers
            var tasks = Enumerable.Range(0, 20).Select(_ =>
                Task.Run(() =>
                    ProtocolJsonSerializer.AddTypeInfoResolver(new DefaultJsonTypeInfoResolver())));

            // Assert: no exceptions
            await Task.WhenAll(tasks);
        }

        // Helper: a resolver that calls an action when it handles the target type
        private sealed class TrackingTypeInfoResolver : IJsonTypeInfoResolver
        {
            private readonly System.Type _targetType;
            private readonly System.Action _onResolved;
            private readonly DefaultJsonTypeInfoResolver _inner = new();

            public TrackingTypeInfoResolver(System.Type targetType, System.Action onResolved)
            {
                _targetType = targetType;
                _onResolved = onResolved;
            }

            public JsonTypeInfo? GetTypeInfo(System.Type type, JsonSerializerOptions options)
            {
                if (type == _targetType)
                    _onResolved();
                return _inner.GetTypeInfo(type, options);
            }
        }
    }
}
