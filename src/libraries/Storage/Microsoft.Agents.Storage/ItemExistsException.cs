// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Storage
{
    public class ItemExistsException : Exception
    {
        public ItemExistsException()
        {
        }

        public ItemExistsException(string key) : base(key)
        {
        }

        public ItemExistsException(string key, Exception innerException) : base(key, innerException)
        {
        }
    }
}
