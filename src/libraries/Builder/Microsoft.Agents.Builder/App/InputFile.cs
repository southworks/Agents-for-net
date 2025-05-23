﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Represents an upload file
    /// </summary>
    public class InputFile
    {
        /// <summary>
        /// The downloaded content of the file
        /// </summary>
        public BinaryData Content { get; set; }

        /// <summary>
        /// The content type of the file.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Optional. URL to the content of the file.
        /// </summary>
        public string? ContentUrl { get; set; }

        /// <summary>
        /// Optional. The file name.
        /// </summary>
        public string? Filename { get; set; }

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="content">The input file content</param>
        /// <param name="contentType">the input file content type</param>
        public InputFile(BinaryData content, string contentType)
        {
            Content = content;
            ContentType = contentType;
        }
    }
}
