﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Utils;
using Microsoft.Agents.Core.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Utils
{
    // We need to disable parallel running as the tests share the AppSettings configuration and step on one another. 
    [CollectionDefinition("SequentialTests", DisableParallelization = true)]
    public class AppSettingsHelperTests
    {
        [Fact]
        public void GetAppSetting_ShouldReturnDefaultValueOnMissingConfig()
        {
            var defaultSetting = 3;

            var setting = AppSettingsHelper.GetAppSetting("testSetting1", defaultSetting);

            Assert.Equal(defaultSetting, setting);
        }

        [Fact]
        public void GetAppSetting_ShouldReturnValueFromConfig()
        {
            var customSetting = 5;
            var defaultSetting = 3;

            System.Configuration.ConfigurationManager.AppSettings.Set("testSetting2", customSetting.ToString());

            var setting = AppSettingsHelper.GetAppSetting("testSetting2", defaultSetting);

            Assert.Equal(customSetting, setting);
        }

        [Fact]
        public void GetAppSetting_ShouldReturnDefaultValueOnError()
        {
            var customSetting = new ChannelAccount() { Id = "test-id" };
            var defaultSetting = new ConversationAccount() { Name = "test-name" };

            System.Configuration.ConfigurationManager.AppSettings.Set("testSetting3", customSetting.ToString());

            var setting = AppSettingsHelper.GetAppSetting("testSetting3", defaultSetting);

            Assert.Equal(defaultSetting, setting);
        }

        [Fact]
        public void GetAppSettingTimeSpan_ShouldReturnDefaultValueOnMissingConfig()
        {
            var defaultSetting = new TimeSpan(0, 0, 0, 45);
            
            var setting = AppSettingsHelper.GetAppSettingTimeSpan("testSetting4", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultSetting);

            Assert.Equal(defaultSetting, setting);
        }

        [Fact]
        public void GetAppSettingTimeSpan_ShouldReturnValueFromConfigOnDifferentFormats()
        {
            var customSetting = 1;
            var defaultSetting = new TimeSpan(0, 0, 0, 45, 0);

            System.Configuration.ConfigurationManager.AppSettings.Set("testSetting5", customSetting.ToString());

            var settingMillisec = AppSettingsHelper.GetAppSettingTimeSpan("testSetting5", AppSettingsHelper.TimeSpanFromKey.Milliseconds, defaultSetting);
            Assert.Equal(TimeSpan.FromMilliseconds(1), settingMillisec);

            var settingSeconds = AppSettingsHelper.GetAppSettingTimeSpan("testSetting5", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultSetting);
            Assert.Equal(TimeSpan.FromSeconds(1), settingSeconds);

            var settingMinutes = AppSettingsHelper.GetAppSettingTimeSpan("testSetting5", AppSettingsHelper.TimeSpanFromKey.Minutes, defaultSetting);
            Assert.Equal(TimeSpan.FromMinutes(1), settingMinutes);

            var settingHours = AppSettingsHelper.GetAppSettingTimeSpan("testSetting5", AppSettingsHelper.TimeSpanFromKey.Hours, defaultSetting);
            Assert.Equal(TimeSpan.FromHours(1), settingHours);

            var settingDays = AppSettingsHelper.GetAppSettingTimeSpan("testSetting5", AppSettingsHelper.TimeSpanFromKey.Days, defaultSetting);
            Assert.Equal(TimeSpan.FromDays(1), settingDays);
        }

        [Fact]
        public void GetAppSettingTimeSpan_ShouldReturnDefaultValueOnParseFailure()
        {
            var customSetting = "two";
            var defaultSetting = new TimeSpan(0, 0, 0, 45);

            System.Configuration.ConfigurationManager.AppSettings.Set("testSetting6", customSetting);

            var setting = AppSettingsHelper.GetAppSettingTimeSpan("testSetting6", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultSetting);

            Assert.Equal(defaultSetting, setting);
        }

        [Fact]
        public void GetAppSettingTimeSpan_ShouldReturnDefaultValueOnError()
        {
            var customSetting = new ChannelAccount() { Id = "test-id" };
            var defaultSetting = new TimeSpan(0, 0, 0, 45);

            System.Configuration.ConfigurationManager.AppSettings.Set("testSetting7", customSetting.ToString());

            var setting = AppSettingsHelper.GetAppSettingTimeSpan("testSetting7", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultSetting);

            Assert.Equal(defaultSetting, setting);
        }

        [Fact]
        public void GetAppSettingTimeSpan_ShouldReturnDefaultValueOnFormatError()
        {
            var customSetting = new ChannelAccount() { Id = "test-id" };
            var defaultSetting = new TimeSpan(2, 0, 0, 0);
            var logger = new Mock<ILogger>();
            logger.Setup(x => x.LogWarning(It.IsAny<string>(), It.IsAny<object[]>())).Throws(new Exception());

            System.Configuration.ConfigurationManager.AppSettings.Set("testSetting8", customSetting.ToString());

            var setting = AppSettingsHelper.GetAppSettingTimeSpan("testSetting8", AppSettingsHelper.TimeSpanFromKey.Days, defaultSetting, logger.Object);

            Assert.Equal(defaultSetting, setting);
        }
    }
}
