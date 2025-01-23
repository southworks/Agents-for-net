// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Utils;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Utils
{
    public class AppSettingsHelperTests
    {
        [Fact]
        public void GetAppSetting_ShouldReturnDefaultValueOnMissingConfig()
        {
            var defaultTimeout = new TimeSpan(0, 0, 0, 45);
            
            var msalTimeout = AppSettingsHelper.GetAppSettingTimeSpan("MSALRequestTimeoutOverride", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultTimeout);

            Assert.Equal(defaultTimeout, msalTimeout);
        }

        //[Fact]
        //public void GetAppSetting_ShouldReturnDefaultValueOnKeyError()
        //{
        //    var configSettings = new Dictionary<string, string> {
        //        { "MSALRequestTimeout", "00:00:01:00" },
        //    };

        //    IConfiguration configuration = new ConfigurationBuilder()
        //        .AddInMemoryCollection(configSettings)
        //        .Build();

        //    var defaultTimeout = new TimeSpan(0, 0, 0, 45);

        //    var msalTimeout = AppSettingsHelper.GetAppSettingTimeSpan("MSALRequestTimeout2", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultTimeout);

        //    Assert.Equal(defaultTimeout, msalTimeout);
        //}

        [Fact]
        public void GetAppSetting_ShouldReturnValueFromConfig()
        {
            //config.AppSettings.Settings.Add("MSALRequestTimeout", "00:00:01:00");

            //config.Save(ConfigurationSaveMode.Modified);
            //System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            System.Configuration.ConfigurationManager.AppSettings.Add("MSALRequestTimeout", "00:00:03:00");
            //var configSettings = new Dictionary<string, string> {
            //    { "AppSettings:MSALRetryCount", "00:00:03:00" },
            //};

            //IConfiguration configuration = new ConfigurationBuilder()
            //    .AddInMemoryCollection(configSettings)
            //    .Build();

            var collection = new NameValueCollection
            {
                { "MSALRetryCount", "5" }
            };

            //System.Configuration.ConfigurationManager.AppSettings. = collection;

            var defaultTimeout = new TimeSpan(0, 0, 0, 45);

            var msalTimeout = AppSettingsHelper.GetAppSettingTimeSpan("MSALRequestTimeout", AppSettingsHelper.TimeSpanFromKey.Seconds, defaultTimeout);

            Assert.Equal(new TimeSpan(0, 0, 1, 00), msalTimeout);
        }
    }
}
