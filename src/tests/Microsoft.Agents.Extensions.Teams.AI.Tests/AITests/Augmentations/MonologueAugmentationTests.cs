﻿using Json.Schema;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.AI.Augmentations;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Moq;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.AITests.Augmentations
{
    public class MonologueAugmentationTests
    {
        [Fact]
        public async Task Test_ValidateResponseAsync_ShouldSucceed()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            MonologueAugmentation augmentation = new(new()
            {
                new("test")
                {
                    Description = "test action",
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                        .Build()
                }
            });

            InnerMonologue monologue = new(new("test", "test"), new("test", new()
            {
                { "foo", "bar" }
            }));

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    Content = JsonSerializer.Serialize(monologue)
                }
            };

            var res = await augmentation.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(res.Valid);
        }

        [Fact]
        public async Task Test_ValidateResponseAsync_InvalidThoughts_ShouldFail()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            MonologueAugmentation augmentation = new(new()
            {
                new("test")
                {
                    Description = "test action",
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                        .Build()
                }
            });

            Dictionary<string, object> monologue = new()
            {
                {
                    "thoughts",
                    new Dictionary<string, object>()
                    {
                        { "reasoning", "test" },
                        { "plan", "test" }
                    }
                },
                {
                    "action",
                    new Dictionary<string, object>()
                    {
                        {
                            "name",
                            "test"
                        },
                        {
                            "parameters",
                            new Dictionary<string, object>()
                            {
                                { "foo", "bar" }
                            }
                        }
                    }
                }
            };

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    Content = JsonSerializer.Serialize(monologue)
                }
            };

            var res = await augmentation.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }

        [Fact]
        public async Task Test_ValidateResponseAsync_InvalidAction_ShouldFail()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            MonologueAugmentation augmentation = new(new()
            {
                new("test")
                {
                    Description = "test action",
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                        .Build()
                }
            });

            Dictionary<string, object> monologue = new()
            {
                {
                    "thoughts",
                    new Dictionary<string, object>()
                    {
                        { "thought", "test" },
                        { "reasoning", "test" },
                        { "plan", "test" }
                    }
                },
                {
                    "action",
                    new Dictionary<string, object>()
                    {
                        {
                            "name",
                            "test"
                        },
                        {
                            "parameters",
                            new Dictionary<string, object>()
                            {
                                { "hello", "world" }
                            }
                        }
                    }
                }
            };

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    Content = JsonSerializer.Serialize(monologue)
                }
            };

            var res = await augmentation.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.False(res.Valid);
        }

        [Fact]
        public async Task Test_CreatePlanFromResponseAsync_SayCommand_ShouldSucceed()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            MonologueAugmentation augmentation = new(new()
            {
                new("test")
                {
                    Description = "test action",
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                        .Build()
                }
            });

            Dictionary<string, object> monologue = new()
            {
                {
                    "thoughts",
                    new Dictionary<string, object>()
                    {
                        { "thought", "test" },
                        { "reasoning", "test" },
                        { "plan", "test" }
                    }
                },
                {
                    "action",
                    new Dictionary<string, object>()
                    {
                        {
                            "name",
                            "SAY"
                        },
                        {
                            "parameters",
                            new Dictionary<string, object>()
                            {
                                { "text", "hello world" }
                            }
                        }
                    }
                }
            };

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    Content = JsonSerializer.Serialize(monologue),
                    Context = new()
                    {
                        Intent = "test intent",
                        Citations = new List<Citation>
                        {
                            new("content", "title", "url")
                        }
                    }
                }
            };

            var valid = await augmentation.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(valid.Valid);

            var plan = await augmentation.CreatePlanFromResponseAsync(context.Object, memory, promptResponse);

            Assert.NotNull(plan);
            Assert.Single(plan.Commands);
            Assert.Equal("SAY", plan.Commands[0].Type);
            Assert.Equal("hello world", (plan.Commands[0] as PredictedSayCommand)?.Response.Content);
            Assert.Equal("test intent", (plan.Commands[0] as PredictedSayCommand)?.Response.Context?.Intent);
            Assert.Equal("content", (plan.Commands[0] as PredictedSayCommand)?.Response.Context?.Citations[0].Content);
            Assert.Equal("title", (plan.Commands[0] as PredictedSayCommand)?.Response.Context?.Citations[0].Title);
            Assert.Equal("url", (plan.Commands[0] as PredictedSayCommand)?.Response.Context?.Citations[0].Url);
        }

        [Fact]
        public async Task Test_CreatePlanFromResponseAsync_DoCommand_ShouldSucceed()
        {
            Mock<ITurnContext> context = new();
            MemoryFork memory = new();
            GPTTokenizer tokenizer = new();
            MonologueAugmentation augmentation = new(new()
            {
                new("test")
                {
                    Description = "test action",
                    Parameters = new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            (
                                "foo",
                                new JsonSchemaBuilder()
                                    .Type(SchemaValueType.String)
                            )
                        )
                        .Required(new string[] { "foo" })
                        .Build()
                }
            });

            Dictionary<string, object> monologue = new()
            {
                {
                    "thoughts",
                    new Dictionary<string, object>()
                    {
                        { "thought", "test" },
                        { "reasoning", "test" },
                        { "plan", "test" }
                    }
                },
                {
                    "action",
                    new Dictionary<string, object>()
                    {
                        {
                            "name",
                            "test"
                        },
                        {
                            "parameters",
                            new Dictionary<string, object>()
                            {
                                { "foo", "bar" }
                            }
                        }
                    }
                }
            };

            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    Content = JsonSerializer.Serialize(monologue)
                }
            };

            var valid = await augmentation.ValidateResponseAsync(context.Object, memory, tokenizer, promptResponse, 0);

            Assert.True(valid.Valid);

            var plan = await augmentation.CreatePlanFromResponseAsync(context.Object, memory, promptResponse);

            Assert.NotNull(plan);
            Assert.Single(plan.Commands);
            Assert.Equal("DO", plan.Commands[0].Type);
            Assert.Equal("test", (plan.Commands[0] as PredictedDoCommand)?.Action);
        }
    }
}
