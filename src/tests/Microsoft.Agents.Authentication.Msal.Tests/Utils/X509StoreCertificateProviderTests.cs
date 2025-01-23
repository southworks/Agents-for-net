﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Msal.Model;
using Microsoft.Agents.Authentication.Msal.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Agents.Authentication.Msal.Tests.Utils
{
    public class X509StoreCertificateProviderTests
    {
        private const string SettingsSection = "Connections:Settings";
        
        private static readonly Dictionary<string, string> ConfigSettings = new Dictionary<string, string> {
            { "Connections:Settings:AuthType", "Certificate" },
            { "Connections:Settings:ClientId", "test-client-id" },
            { "Connections:Settings:AuthorityEndpoint", "test-authority" },
            { "Connections:Settings:TenantId", "test-tenant-id" },
            { "Connections:Settings:CertificateStoreName", "My" },
            { "Connections:Settings:CertThumbprint", "test-thumbprint" },
        };

        private static readonly IConfiguration Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(ConfigSettings)
            .Build();

        private readonly Mock<ILogger<MsalAuth>> Logger = new Mock<ILogger<MsalAuth>>();

        private readonly ConnectionSettings ConnectionSettings = new ConnectionSettings(Configuration.GetSection(SettingsSection));

        [Fact]
        public void GetCertificate_ShouldReturnCertificate()
        {
            var testCertificate = CreateSelfSignedCertificate("SelfSignedCert");

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(testCertificate);
                store.Close();
            }

            var thumbprint = testCertificate.Thumbprint;

            var settings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "Certificate" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "test-authority" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
                { "Connections:Settings:CertificateStoreName", "My" },
                { "Connections:Settings:CertThumbprint", thumbprint },
                { "Connections:Settings:ValidCertificateOnly", "false" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

            var connectionSettings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            var provider = new X509StoreCertificateProvider(connectionSettings, Logger.Object);

            var certificate = provider.GetCertificate();

            Assert.Equal(testCertificate, certificate);

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Remove(testCertificate);
                store.Close();
            }
        }

        [Fact]
        public void GetCertificate_ShouldReturnNullForCertificateNotFound()
        {
            var provider = new X509StoreCertificateProvider(ConnectionSettings, Logger.Object);

            var certificate = provider.GetCertificate();

            Assert.Null(certificate);
        }

        [Fact]
        public void GetCertificate_ShouldReturnCertificateSubjectName()
        {
            var testCertificate = CreateSelfSignedCertificate("test-cert");

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(testCertificate);
                store.Close();
            }

            var settings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "CertificateSubjectName" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "test-authority" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
                { "Connections:Settings:CertificateStoreName", "My" },
                { "Connections:Settings:ValidCertificateOnly", "false" },
                { "Connections:Settings:CertSubjectName", "test-cert" },
            };

            IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

            var connectionSettings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            var provider = new X509StoreCertificateProvider(connectionSettings, Logger.Object);

            var certificate = provider.GetCertificate();

            Assert.Equal(testCertificate, certificate);

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Remove(testCertificate);
                store.Close();
            }
        }

        [Fact]
        public void GetCertificate_ShouldReturnNullForCertificateSubjectNameNotFound()
        {
            var settings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "CertificateSubjectName" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:AuthorityEndpoint", "test-authority" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
                { "Connections:Settings:CertSubjectName", "NotFound" }
            };

            IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

            var connectionSettings = new ConnectionSettings(configuration.GetSection(SettingsSection));

            var provider = new X509StoreCertificateProvider(connectionSettings, Logger.Object);

            var certificate = provider.GetCertificate();

            Assert.Null(certificate);
        }

        [Fact]
        public void GetCertificate_ShouldReturnNullOnWrongAuthType()
        {
            var settings = new Dictionary<string, string> {
                { "Connections:Settings:AuthType", "ClientSecret" },
                { "Connections:Settings:ClientId", "test-client-id" },
                { "Connections:Settings:ClientSecret", "test-client-secret" },
                { "Connections:Settings:TenantId", "test-tenant-id" },
            };

            IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

            var connectionSettings = new ConnectionSettings(config.GetSection(SettingsSection));

            var provider = new X509StoreCertificateProvider(connectionSettings, Logger.Object);

            var certificate = provider.GetCertificate();

            Assert.Null(certificate);
        }

        [Fact]
        public void GetStoreName_ShouldReturnDefaultOnInvalidName()
        {

            var provider = new X509StoreCertificateProvider(ConnectionSettings, Logger.Object);

            var storeName = provider.GetStoreName("test-name");

            Assert.Equal(StoreName.My, storeName);
        }

        [Fact]
        public void GetStoreName_ShouldReturnDefaultOnNullName()
        {
            var provider = new X509StoreCertificateProvider(ConnectionSettings, Logger.Object);

            var storeName = provider.GetStoreName(null);

            Assert.Equal(StoreName.My, storeName);
        }

        [Fact]
        public void GetStoreName_ShouldReturnProvidedName()
        {
            var provider = new X509StoreCertificateProvider(ConnectionSettings, Logger.Object);

            var storeName = provider.GetStoreName(StoreName.TrustedPeople.ToString());

            Assert.Equal(StoreName.TrustedPeople, storeName);
        }

        public static X509Certificate2 CreateSelfSignedCertificate(string subjectName)
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest($"CN={subjectName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));

            return certificate;
        }
    }
}
