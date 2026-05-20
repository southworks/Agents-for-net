// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Transport
{
    /// <summary>
    /// Serializes and deserializes the 48-byte ASCII header used by the
    /// Bot Framework named pipe protocol.
    /// </summary>
    internal static class HeaderSerializer
    {
        /// <summary>
        /// The fixed size (in bytes) of a wire frame header.
        /// </summary>
        public const int HeaderSize = 48;

        /// <summary>
        /// Serialize a header to its 48-byte ASCII wire representation.
        /// </summary>
        /// <param name="header">The header to serialize.</param>
        /// <param name="buffer">A buffer of at least <see cref="HeaderSize"/> bytes.</param>
        /// <exception cref="ArgumentException">Thrown when buffer is too small.</exception>
        public static void Serialize(Header header, Span<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
            {
                throw new ArgumentException($"Buffer must be at least {HeaderSize} bytes.", nameof(buffer));
            }

            // Format: {Type}.{Length:6}.{Id:36}.{End}\n
            buffer[0] = (byte)header.Type;
            buffer[1] = (byte)'.';

            var lengthStr = header.PayloadLength.ToString("D6");
            Encoding.ASCII.GetBytes(lengthStr, buffer[2..8]);
            buffer[8] = (byte)'.';

            var idStr = header.Id.ToString("D"); // 36 chars
            Encoding.ASCII.GetBytes(idStr, buffer[9..45]);
            buffer[45] = (byte)'.';

            buffer[46] = (byte)(header.End ? '1' : '0');
            buffer[47] = (byte)'\n';
        }

        /// <summary>
        /// Deserialize a 48-byte ASCII header from the wire.
        /// </summary>
        /// <param name="buffer">A buffer containing at least <see cref="HeaderSize"/> bytes.</param>
        /// <returns>The deserialized header.</returns>
        /// <exception cref="ArgumentException">Thrown when buffer is too small.</exception>
        /// <exception cref="FormatException">Thrown when the header data is malformed.</exception>
        public static Header Deserialize(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < HeaderSize)
            {
                throw new ArgumentException($"Buffer must be at least {HeaderSize} bytes.", nameof(buffer));
            }

            var type = (char)buffer[0];

            var lengthStr = Encoding.ASCII.GetString(buffer[2..8]);
            if (!int.TryParse(lengthStr, out var payloadLength) || payloadLength < 0)
            {
                throw new FormatException($"Invalid payload length: '{lengthStr}'");
            }

            var idStr = Encoding.ASCII.GetString(buffer[9..45]);
            if (!Guid.TryParse(idStr, out var id))
            {
                throw new FormatException($"Invalid GUID: '{idStr}'");
            }

            var end = (char)buffer[46] == '1';

            return new Header
            {
                Type = type,
                PayloadLength = payloadLength,
                Id = id,
                End = end
            };
        }

        /// <summary>
        /// Serialize a header to a new byte array.
        /// </summary>
        /// <param name="header">The header to serialize.</param>
        /// <returns>A 48-byte array containing the serialized header.</returns>
        public static byte[] Serialize(Header header)
        {
            var buffer = new byte[HeaderSize];
            Serialize(header, buffer);
            return buffer;
        }
    }
}
