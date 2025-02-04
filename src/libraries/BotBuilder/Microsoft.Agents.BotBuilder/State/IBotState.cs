// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.BotBuilder.State
{
    public interface IBotState
    {
        string Name { get; }

        void ClearState();
        Task DeleteStateAsync(ITurnContext turnContext, CancellationToken cancellationToken = default);
        void DeleteValue(string name);
        T GetValue<T>(string name, Func<T> defaultValueFactory = null);
        bool IsLoaded();
        Task LoadAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
        void SetValue<T>(string name, T value);
        bool HasValue(string name);
    }
}