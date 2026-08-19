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
        /// When <see langword="false"/> (the default), queued Activities resolve the
        /// <see cref="Microsoft.Agents.Builder.IAgent"/> from the root <see cref="System.IServiceProvider"/>.
        /// Set this to <see langword="true"/> to resolve the Agent and its dependencies from a scope that is disposed when
        /// the turn completes.
        /// </remarks>
        public bool UseScopedServices { get; set; } = false;
    }
}
