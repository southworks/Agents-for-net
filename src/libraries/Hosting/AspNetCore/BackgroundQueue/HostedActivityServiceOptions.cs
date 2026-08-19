// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Configuration;
using System;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Configuration options for the hosted activity service.
    /// </summary>
    public class HostedActivityServiceOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HostedActivityServiceOptions"/> class.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        public HostedActivityServiceOptions(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.GetSection(nameof(HostedActivityServiceOptions)).Bind(this);
        }

        /// <summary>
        /// Gets or sets the maximum number of seconds to wait for activity processing during shutdown.
        /// </summary>
        public int ShutdownTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets a value indicating whether each queued Activity is processed in its own dependency injection scope.
        /// </summary>
        /// <remarks>
        /// When <see langword="false"/> (the default), queued Activities resolve the <see cref="Microsoft.Agents.Builder.IAgent"/>
        /// from the root <see cref="System.IServiceProvider"/>. Any scoped registration in the Agent's dependency graph is then
        /// promoted to the root scope, giving a single instance shared by every turn for the lifetime of the process.
        /// Set this to <see langword="true"/> for Agents that depend on scoped services, such as an Entity Framework Core
        /// <c>DbContext</c>. The SDK registers no scoped services, so enabling this affects only registrations made by the
        /// application. Note that disposable transient dependencies resolved for a turn - <see cref="Microsoft.Agents.Builder.IAgent"/>
        /// itself is registered transient - are then disposed with the turn scope, rather than being retained by the root
        /// scope until the host shuts down.
        /// </remarks>
        public bool UseScopedServices { get; set; } = false;
    }
}
