// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Protocols.Primitives;
using System;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class TurnContextStateCollectionTests
    {
        [Fact]
        public void Get_ThrowsOnDisposed()
        {
            var stateCollection = new TurnContextStateCollection();
            stateCollection.Add("test", new object());

            stateCollection.Dispose();
            Assert.Throws<ObjectDisposedException>(() => stateCollection.Get<object>("test"));
        }

        [Fact]
        public void Get_ThrowsOnNullKey()
        {
            var stateCollection = new TurnContextStateCollection();

            Assert.Throws<ArgumentNullException>(() => stateCollection.Get<object>(null));
        }

        [Fact]
        public void Add_ThrowsOnDisposed()
        {
            var stateCollection = new TurnContextStateCollection();

            stateCollection.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stateCollection.Add("test", new object()));
        }

        [Fact]
        public void Add_ThrowsOnNullKey()
        {
            var stateCollection = new TurnContextStateCollection();
            Assert.Throws<ArgumentNullException>(() => stateCollection.Add(null, new object()));
        }

        [Fact]
        public void Add_ThrowsOnNullValue()
        {
            var stateCollection = new TurnContextStateCollection();
            var test = new object();
            test = null;
            Assert.Throws<ArgumentNullException>(() => stateCollection.Add("test", test));
        }

        [Fact]
        public void Add_ShouldIncludeElementsInCollection()
        {
            var stateCollection = new TurnContextStateCollection();
            var test = new object();
            var test2 = new object();

            stateCollection.Add("test", test);
            stateCollection.Add(test2);
            Assert.Equal(test, stateCollection.Get<object>("test"));
            Assert.Equal(test2, stateCollection.Get<object>());
            Assert.NotEqual(test, stateCollection.Get<object>());
        }

        [Fact]
        public void Set_ThrowsOnDisposed()
        {
            var stateCollection = new TurnContextStateCollection();

            stateCollection.Dispose();

            Assert.Throws<ObjectDisposedException>(() => stateCollection.Set("test", new object()));
        }

        [Fact]
        public void Set_ThrowsOnNullKey()
        {
            var stateCollection = new TurnContextStateCollection();
            Assert.Throws<ArgumentNullException>(() => stateCollection.Set(null, new object()));
        }

        [Fact]
        public void Set_ProperlySetTheValues()
        {
            var stateCollection = new TurnContextStateCollection();
            var test = new object();
            var test2 = new object();

            stateCollection.Set("test", test);
            stateCollection.Set(test2);
            Assert.Equal(test, stateCollection.Get<object>("test"));
            Assert.Equal(test2, stateCollection.Get<object>());
            Assert.NotEqual(test, stateCollection.Get<object>());

            stateCollection.Set<object>("test", null);
            Assert.Null(stateCollection.Get<object>("test"));
            Assert.Equal(test2, stateCollection.Get<object>());
            stateCollection.Set<object>(null);
            Assert.Null(stateCollection.Get<object>());
        }
    }
}
