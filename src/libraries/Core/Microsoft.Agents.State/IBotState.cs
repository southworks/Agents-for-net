// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.State
{
    public interface IBotState : IMemory
    {
        string Name { get; }

        void ClearState();
        Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
        bool IsLoaded();
        Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
    }
}