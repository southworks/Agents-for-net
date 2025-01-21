// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.State.Tests
{
    public class BotStateSetTests
    {
        [Fact]
        public void TurnState_Properties()
        {
            var storage = new MemoryStorage();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var turnState = new BotStateSet(userState, convState);

            Assert.IsType<UserState>(turnState.GetScope(userState.ContextServiceKey));
            Assert.IsType<ConversationState>(turnState.GetScope(convState.ContextServiceKey));
        }

        [Fact]
        public async Task TurnState_LoadAsync()
        {
            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();
            {
                // setup userstate
                var userState = new UserState(storage);

                // setup convState
                var convState = new ConversationState(storage);

                var turnState = new BotStateSet(userState, convState);
                await turnState.LoadAllAsync(turnContext, false);

                var userCount = turnState.GetValue(turnContext, "user.userCount", () => 0);
                Assert.Equal(0, userCount);
                var convCount = turnState.GetValue(turnContext, "conversation.convCount", () => 0);
                Assert.Equal(0, convCount);

                turnState.SetValue(turnContext, "user.userCount", 10);
                turnState.SetValue(turnContext, "conversation.convCount", 20);

                Assert.Equal(10, turnState.GetValue(turnContext, "user.userCount", () => 0));
                Assert.Equal(20, turnState.GetValue(turnContext, "conversation.convCount", () => 0));

                await turnState.SaveAllChangesAsync(turnContext, false);
            }

            {
                // setup userstate
                var userState = new UserState(storage);

                // setup convState
                var convState = new ConversationState(storage);

                var turnState = new BotStateSet(userState, convState);

                await turnState.LoadAllAsync(turnContext);

                var userCount = turnState.GetValue(turnContext, "user.userCount", () => 0);
                Assert.Equal(10, userCount);
                var convCount = turnState.GetValue(turnContext, "conversation.convCount", () => 0);
                Assert.Equal(20, convCount);
            }
        }

        [Fact]
        public async Task TurnState_DottedProperties()
        {
            var test = new
            {
                test = "test",

                options = new
                {
                    Age = 15,
                    FirstName = "joe",
                    LastName = "blow",
                    Bool = false,
                    Nicknames = new List<string>( [ "Tom", "Rex" ] )
                },

                bar = new
                {
                    numIndex = 2,
                    strIndex = "FirstName",
                    objIndex = "options",
                    options = new Options()
                    {
                        Age = 1,
                        FirstName = "joe",
                        LastName = "blow",
                        Bool = false,
                    },
                    numbers = new int[] { 1, 2, 3, 4, 5 }
                },
            };

            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();
            {
                var userState = new UserState(storage);
                var convState = new ConversationState(storage);
                var turnState = new BotStateSet(userState, convState);

                await turnState.LoadAllAsync(turnContext, false);

                // Add a couple array elements
                turnState.SetValue(turnContext, "conversation.x.a[1]", "yabba");
                turnState.SetValue(turnContext, "conversation.x.a[0]", "dabba");

                Assert.Equal("dabba", turnState.GetValue(turnContext, "conversation.x.a[0]", () => string.Empty));
                Assert.Equal("yabba", turnState.GetValue(turnContext, "conversation.x.a[1]", () => string.Empty));

                // Verify array
                var array = turnState.GetValue(turnContext, "conversation.x.a", () => Array.Empty<string>());
                Assert.IsAssignableFrom<Array>(array);
                Assert.Equal(2, array.Length);
                Assert.Equal("dabba", array[0]);
                Assert.Equal("yabba", array[1]);

                // Anonymous type access
                turnState.SetValue(turnContext, "user.test", test);

                Assert.Equal("FirstName", turnState.GetValue(turnContext, "user.test.bar.strIndex", () => string.Empty));
                Assert.Equal("Rex", turnState.GetValue(turnContext, "user.test.options.Nicknames[1]", () => string.Empty));
                Assert.Equal(2, turnState.GetValue(turnContext, "user.test.bar.numbers[1]", () => -1));

                // Anonymous types are read only
                Assert.Throws<ArgumentException>(() => turnState.SetValue(turnContext, "user.test.bar.strIndex", "NewFirstName"));

                // Don't support growing native arrays yet
                Assert.Throws<ArgumentException>(() => turnState.SetValue(turnContext, "user.test.bar.numbers[10]", 10));

                // But it can grow lists
                turnState.SetValue(turnContext, "user.test.options.Nicknames[5]", "John");
                Assert.Equal("John", turnState.GetValue(turnContext, "user.test.options.Nicknames[5]", () => string.Empty));

                // Can set poco
                var poco = new TestPocoState()
                {
                    Value = "firstValue"
                };
                turnState.SetValue(turnContext, "user.poco", poco);
                Assert.Equal("firstValue", turnState.GetValue(turnContext, "user.poco.Value", () => string.Empty));

                // Get poco object
                var pocoOut = turnState.GetValue(turnContext, "user.poco", () => new TestPocoState());
                Assert.Equal("firstValue", pocoOut.Value);

                // Can set poco field
                turnState.SetValue(turnContext, "user.poco.Value", "secondValue");
                Assert.Equal("secondValue", turnState.GetValue(turnContext, "user.poco.Value", () => string.Empty));

                // List
                turnState.SetValue(turnContext, "conversation.chatHistory", new List<string>());
                var chatHistory = turnState.GetValue(turnContext, "conversation.chatHistory", () => new List<string>());
                chatHistory.Add("Hello");
                chatHistory.Add("Howdy");

                Assert.Equal("Hello", turnState.GetValue(turnContext, "conversation.chatHistory[0]", () => string.Empty));
                Assert.Equal("Howdy", turnState.GetValue(turnContext, "conversation.chatHistory[1]", () => string.Empty));
            }
        }
         
        [Fact]
        public async Task TurnState_ReturnsDefaultForNullValueType()
        {
            var storage = new MemoryStorage();

            var turnContext = TestUtilities.CreateEmptyContext();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var turnState = new BotStateSet(userState, convState);
            await turnState.LoadAllAsync(turnContext, false);

            var userObject = turnState.GetValue<string>(turnContext, "user.userStateObject", () => null);
            Assert.Null(userObject);

            // Ensure we also get null on second attempt
            userObject = turnState.GetValue<string>(turnContext, "user.userStateObject", () => null);
            Assert.Null(userObject);

            var convObject = turnState.GetValue<string>(turnContext, "conversation.convStateObject", () => null);
            Assert.Null(convObject);

            // Ensure we also get null on second attempt
            convObject = turnState.GetValue<string>(turnContext, "conversation.convStateObject", () => null);
            Assert.Null(convObject);
        }

        [Fact]
        public async Task TurnState_SaveAsync()
        {
            var storage = new MemoryStorage();

            // setup userstate
            var userState = new UserState(storage);

            // setup convState
            var convState = new ConversationState(storage);

            var turnState = new BotStateSet(userState, convState);

            var context = TestUtilities.CreateEmptyContext();
            await turnState.LoadAllAsync(context);

            var userCount = userState.GetValue("userCount", () => 0);
            Assert.Equal(0, userCount);
            var convCount = convState.GetValue("convCount", () => 0);
            Assert.Equal(0, convCount);

            userState.SetValue("userCount", 10);
            convState.SetValue("convCount", 20);

            await turnState.SaveAllChangesAsync(context);

            userCount = userState.GetValue("userCount", () => 0);
            Assert.Equal(10, userCount);

            convCount = convState.GetValue("convCount", () => 0);
            Assert.Equal(20, convCount);
        }

        internal class SomeComplexType
        {
            public string PropA { get; set; }

            public int PropB { get; set; }
        }
    }
}
