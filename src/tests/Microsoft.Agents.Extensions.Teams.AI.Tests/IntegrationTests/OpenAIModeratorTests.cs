﻿using Microsoft.Agents.Extensions.Teams.AI.Moderator;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;
using Xunit.Abstractions;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Tests.TestUtils;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Extensions.Teams.AI.Tests.IntegrationTests
{
    public sealed class OpenAIModeratorTests
    {
        private readonly IConfigurationRoot _configuration;
        private readonly RedirectOutput _output;
        private readonly ILoggerFactory _loggerFactory;

        public OpenAIModeratorTests(ITestOutputHelper output)
        {
            _output = new RedirectOutput(output);
            _loggerFactory = new TestLoggerFactory(_output);

            var currentAssemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrWhiteSpace(currentAssemblyDirectory))
            {
                throw new InvalidOperationException("Unable to determine current assembly directory.");
            }

            var directoryPath = Path.GetFullPath(Path.Combine(currentAssemblyDirectory, $"../../../IntegrationTests/"));
            var settingsPath = Path.Combine(directoryPath, "testsettings.json");

            _configuration = new ConfigurationBuilder()
                .AddJsonFile(path: settingsPath, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddUserSecrets<OpenAIModeratorTests>()
                .Build();
        }

        // TODO: There exists a race condition bug where this test fails when running the entire test suite, but not when run in isolation.
        [Theory(Skip = "This test should only be run manually.")]
        [InlineData("I want to kill them.", true)]
        public async Task OpenAIModerator_ReviewPrompt(string input, bool flagged)
        {
            // Arrange
            var config = _configuration.GetSection("OpenAI").Get<OpenAIConfiguration>();
            var options = new OpenAIModeratorOptions(config?.ApiKey ?? throw new Exception("config Missing in OpenAIModerator_ReviewPrompt"), ModerationType.Both);
            var moderator = new OpenAIModerator<TurnState>(options, _loggerFactory);

            var botAdapterMock = new Mock<IChannelAdapter>();
            // TODO: when TurnState is implemented, get the user input
            var activity = new Activity()
            {
                Text = input,
            };
            var turnContext = new TurnContext(botAdapterMock.Object, activity);
            var turnStateMock = new Mock<TurnState>();

            // Act
            var result = await moderator.ReviewInputAsync(turnContext, turnStateMock.Object);

            // Assert
            if (flagged)
            {
                Assert.NotNull(result);
                Assert.Equal(AIConstants.DoCommand, result.Commands[0].Type);
                Assert.Equal(AIConstants.FlaggedInputActionName, ((PredictedDoCommand)result.Commands[0]).Action);
            }
            else
            {
                Assert.Null(result);
            }
        }

        [Theory(Skip = "This test should only be run manually.")]
        [InlineData("I want to kill them.", true)]
        public async Task OpenAIModerator_ReviewPlan(string response, bool flagged)
        {
            // Arrange
            var config = _configuration.GetSection("OpenAI").Get<OpenAIConfiguration>();
            var options = new OpenAIModeratorOptions(config?.ApiKey ?? throw new Exception("config Missing in OpenAIModerator_ReviewPrompt"), ModerationType.Both);
            var moderator = new OpenAIModerator<TurnState>(options, _loggerFactory);

            var turnContextMock = new Mock<ITurnContext>();
            var turnStateMock = new Mock<TurnState>();
            var plan = new Plan(new List<IPredictedCommand>()
            {
                new PredictedSayCommand(response)
            });

            // Act
            var result = await moderator.ReviewOutputAsync(turnContextMock.Object, turnStateMock.Object, plan);

            // Assert
            if (flagged)
            {
                Assert.Equal(AIConstants.DoCommand, result.Commands[0].Type);
                Assert.Equal(AIConstants.FlaggedOutputActionName, ((PredictedDoCommand)result.Commands[0]).Action);
            }
            else
            {
                Assert.Equal(AIConstants.SayCommand, result.Commands[0].Type);
            }
        }
    }
}
