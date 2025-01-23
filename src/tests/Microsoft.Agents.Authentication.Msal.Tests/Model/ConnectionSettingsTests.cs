// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Model
{
    public class ConnectionSettingsTests
    {
        private Dictionary<string, string> configSettings;
        private const string SettingsSection = "Connections:Settings";

        public ConnectionSettingsTests()
        {
            configSettings = new Dictionary<string, string> {
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:ClientSecret", "test-client-secret" },
                { "Connections:Settings:CertThumbprint", "test-thumbprint" },
                { "Connections:Settings:CertificateSubjectName", "test-subject-name" },
                { "Connections:Settings:Authority", "test-authority" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
            };            
        }

        [Fact]
        public void ConnectionSettings_ShouldDefaultToClientSecretType()
        {
            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            var settings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            Assert.Equal(AuthTypes.ClientSecret, settings.AuthType);
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForCertificateType()
        {
            configSettings.Add("Connections:Settings:AuthType", "Certificate");
            configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullCertificateThumbPrintForCertificateType()
        {
            configSettings.Add("Connections:Settings:AuthType", "Certificate");
            configSettings.Remove("Connections:Settings:CertThumbprint");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullAuthorityForCertificateType()
        {
            configSettings.Add("Connections:Settings:AuthType", "Certificate");
            configSettings.Remove("Connections:Settings:Authority");
            configSettings.Remove("Connections:Settings:TenantId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForCertificateSubjectNameType()
        {
            configSettings.Add("Connections:Settings:AuthType", "CertificateSubjectName");
            configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullCertificateSubjectNameForCertificateSubjectNameType()
        {
            configSettings.Add("Connections:Settings:AuthType", "CertificateSubjectName");
            configSettings.Remove("Connections:Settings:CertificateSubjectName");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullAuthorityForCertificateSubjectNameType()
        {
            configSettings.Add("Connections:Settings:AuthType", "CertificateSubjectName");
            configSettings.Remove("Connections:Settings:Authority");
            configSettings.Remove("Connections:Settings:TenantId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForClientSecretType()
        {
            configSettings.Add("Connections:Settings:AuthType", "ClientSecret");
            configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientSecretForClientSecretType()
        {
            configSettings.Add("Connections:Settings:AuthType", "ClientSecret");
            configSettings.Remove("Connections:Settings:ClientSecret");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullAuthorityForClientSecretType()
        {
            configSettings.Add("Connections:Settings:AuthType", "ClientSecret");
            configSettings.Remove("Connections:Settings:Authority");
            configSettings.Remove("Connections:Settings:TenantId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }

        [Fact]
        public void ValidateConfiguration_ShouldThrowOnNullClientIdForUserManagedIdentityType()
        {
            configSettings.Add("Connections:Settings:AuthType", "UserManagedIdentity");
            configSettings.Remove("Connections:Settings:ClientId");

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configSettings)
                .Build();

            Assert.Throws<ArgumentNullException>(() => new ConnectionSettings(configuration.GetSection(SettingsSection)));
        }
    }
}
