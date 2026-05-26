// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    /// <summary>
    /// <para>
    /// Tests for DictionaryOfObjectConverter serialization of primitive arrays.
    /// Validates that IDictionary&lt;string, object&gt; containing arrays of primitive
    /// types (string[], int[], bool[]) can be serialized without throwing.
    /// </para>
    /// <para>
    /// Regression tests for: SerializeJsonArray calls AddTypeInfo on every array
    /// element via jsonNode.AsObject(), which throws InvalidOperationException
    /// when the element is a JsonValue (primitives) rather than a JsonObject.
    /// </para>
    /// </summary>
    public class DictionaryOfObjectConverterTests
    {
        private static readonly JsonSerializerOptions SdkOptions = ProtocolJsonSerializer.SerializationOptions;

        #region Primitive arrays in dictionary values

        [Fact]
        public void Serialize_StringArray_InDictionary_Succeeds()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["parameters"] = new string[] { "param1", "param2", "param3" }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("param1", json);
            Assert.Contains("param2", json);
            Assert.Contains("param3", json);
        }

        [Fact]
        public void Serialize_IntArray_InDictionary_Succeeds()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["counters"] = new int[] { 1, 2, 3 }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("1", json);
            Assert.Contains("2", json);
            Assert.Contains("3", json);
        }

        [Fact]
        public void Serialize_BoolArray_InDictionary_Succeeds()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["flags"] = new bool[] { true, false, true }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("true", json);
            Assert.Contains("false", json);
        }

        #endregion

        #region Simulated WaterfallDialog state with primitive array options

        [Fact]
        public void Serialize_WaterfallState_WithStringArrayOptions_Succeeds()
        {
            // Simulates WaterfallDialog storing string[] as state["options"]
            // in DialogInstance.State (IDictionary<string, object>)
            var waterfallState = new Dictionary<string, object>
            {
                ["options"] = new string[] { "AddressNormalizerResponse", "AzureMaps" },
                ["values"] = new Dictionary<string, object>(),
                ["instanceId"] = "4db4c090-cd48-4682-9810-b6590f2bd72f",
                ["stepIndex"] = 0
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(waterfallState, SdkOptions);

            Assert.Contains("options", json);
            Assert.Contains("AddressNormalizerResponse", json);
            Assert.Contains("AzureMaps", json);
        }

        [Fact]
        public void Serialize_WaterfallState_WithStringArrayInValues_Succeeds()
        {
            // Simulates stepContext.Values containing a string[]
            var waterfallState = new Dictionary<string, object>
            {
                ["options"] = (object)null,
                ["values"] = new Dictionary<string, object>
                {
                    ["ApiParameters"] = new string[] { "param1", "param2", "param3" }
                },
                ["instanceId"] = "test-instance-id",
                ["stepIndex"] = 0
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(waterfallState, SdkOptions);

            Assert.Contains("ApiParameters", json);
        }

        [Fact]
        public void Serialize_WaterfallState_WithStringArrayInBothOptionsAndValues_Succeeds()
        {
            // Both options AND values contain string[] — double vulnerability scenario
            var waterfallState = new Dictionary<string, object>
            {
                ["options"] = new string[] { "AddressNormalizerResponse", "GoogleMaps" },
                ["values"] = new Dictionary<string, object>
                {
                    ["ApiParameters"] = new string[] { "AddressNormalizerResponse", "GoogleMaps", "{Street}" }
                },
                ["instanceId"] = "test-instance-id",
                ["stepIndex"] = 0
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(waterfallState, SdkOptions);

            Assert.Contains("options", json);
            Assert.Contains("ApiParameters", json);
        }

        #endregion

        #region JsonElement arrays (from STJ deserialization of JSON payloads)

        [Fact]
        public void Serialize_JsonElementArray_InDictionary_Succeeds()
        {
            // Simulates an object property deserialized as JsonElement (common with
            // STJ when the target type is 'object'). A JsonElement of kind Array
            // is not IList, so the converter must handle it gracefully.
            var jsonElementArray = JsonSerializer.Deserialize<JsonElement>("""["param1", "param2"]""");

            var dictionary = new Dictionary<string, object>
            {
                ["options"] = jsonElementArray,
                ["stepIndex"] = 0
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("param1", json);
            Assert.Contains("param2", json);
        }

        #endregion

        #region Roundtrip: primitive arrays survive serialize/deserialize

        [Fact]
        public void Roundtrip_StringArray_InDictionary()
        {
            var original = new string[] { "alpha", "beta", "gamma" };
            var dictionary = new Dictionary<string, object>
            {
                ["items"] = original
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);
            IDictionary<string, object> deserialized = JsonSerializer.Deserialize<IDictionary<string, object>>(json, SdkOptions);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.ContainsKey("items"));
        }

        [Fact]
        public void Roundtrip_StringArray_InDictionary_PreservesValues()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["items"] = new string[] { "alpha", "beta", "gamma" }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);
            IDictionary<string, object> deserialized = JsonSerializer.Deserialize<IDictionary<string, object>>(json, SdkOptions);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.ContainsKey("items"));
            IList items = (IList)deserialized["items"];
            Assert.Equal(3, items.Count);
            Assert.Equal("alpha", items[0].ToString());
            Assert.Equal("beta", items[1].ToString());
            Assert.Equal("gamma", items[2].ToString());
        }

        [Fact]
        public void Roundtrip_IntArray_InDictionary_PreservesValues()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["counters"] = new int[] { 10, 20, 30 }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);
            IDictionary<string, object> deserialized = JsonSerializer.Deserialize<IDictionary<string, object>>(json, SdkOptions);

            Assert.NotNull(deserialized);
            Assert.True(deserialized.ContainsKey("counters"));
            IList counters = (IList)deserialized["counters"];
            Assert.Equal(3, counters.Count);
            Assert.Equal(10, Convert.ToInt32(counters[0]));
            Assert.Equal(20, Convert.ToInt32(counters[1]));
            Assert.Equal(30, Convert.ToInt32(counters[2]));
        }

        [Fact]
        public void Roundtrip_WaterfallState_WithStringArray_PreservesStructure()
        {
            var waterfallState = new Dictionary<string, object>
            {
                ["options"] = new string[] { "AddressNormalizerResponse", "AzureMaps" },
                ["values"] = new Dictionary<string, object>
                {
                    ["ApiParameters"] = new string[] { "param1", "param2" }
                },
                ["instanceId"] = "test-id",
                ["stepIndex"] = 0
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(waterfallState, SdkOptions);
            IDictionary<string, object> deserialized = JsonSerializer.Deserialize<IDictionary<string, object>>(json, SdkOptions);

            Assert.NotNull(deserialized);
            IList options = (IList)deserialized["options"];
            Assert.Equal(2, options.Count);
            Assert.Equal("AddressNormalizerResponse", options[0].ToString());
            Assert.Equal("AzureMaps", options[1].ToString());
            Assert.Equal("test-id", deserialized["instanceId"].ToString());
        }

        [Fact]
        public void Roundtrip_ObjectArray_InDictionary_PreservesTypeInfo()
        {
            // Arrays of complex objects should still get type info as before
            var dictionary = new Dictionary<string, object>
            {
                ["items"] = new object[] { new { Name = "foo" }, new { Name = "bar" } }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("foo", json);
            Assert.Contains("bar", json);
        }

        #endregion

        #region Mixed content dictionaries

        [Fact]
        public void Serialize_MixedDictionary_WithPrimitivesObjectsAndArrays_Succeeds()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["stringVal"] = "hello",
                ["intVal"] = 42,
                ["boolVal"] = true,
                ["nullVal"] = (object)null,
                ["stringArray"] = new string[] { "a", "b", "c" },
                ["intArray"] = new int[] { 1, 2, 3 },
                ["objectArray"] = new object[] { new { X = 1 }, new { X = 2 } },
                ["nestedDict"] = new Dictionary<string, object>
                {
                    ["inner"] = "value"
                }
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("hello", json);
            Assert.Contains("42", json);
            Assert.Contains("true", json);
            Assert.Contains("\"a\"", json);
        }

        #endregion

        #region Empty arrays (should always work)

        [Fact]
        public void Serialize_EmptyStringArray_InDictionary_Succeeds()
        {
            var dictionary = new Dictionary<string, object>
            {
                ["empty"] = Array.Empty<string>()
            };

            var json = JsonSerializer.Serialize<IDictionary<string, object>>(dictionary, SdkOptions);

            Assert.Contains("empty", json);
        }

        #endregion
    }
}
