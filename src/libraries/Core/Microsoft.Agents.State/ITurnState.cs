// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Agents.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.State
{
    public interface ITurnState
    {
        ConversationState Conversation { get; }
        PrivateConversationState Private { get; }
        TempState Temp { get; }
        UserState User { get; }

        IBotState GetScope(string scope);
        T GetScope<T>();

        T GetValue<T>(string path, Func<T> defaultValueFactory = null);
        void SetValue(string path, object value);
        void DeleteValue(string path);
        bool HasValue(string path);

        void ClearState(string scope);
        Task LoadStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
        Task SaveStateAsync(ITurnContext turnContext, bool force = false, CancellationToken cancellationToken = default);
    }
}