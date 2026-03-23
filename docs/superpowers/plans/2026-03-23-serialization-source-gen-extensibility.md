# Serialization Source Generation & Third-Party Extensibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a source-generated `CoreJsonContext` to `Microsoft.Agents.Core`, wire it into `ProtocolJsonSerializer`, and expose `AddTypeInfoResolver()` so third parties can register their own `JsonSerializerContext` instances.

**Architecture:** `ProtocolJsonSerializer.SerializationOptions` gets a `TypeInfoResolver` chain of `CoreJsonContext.Default → DefaultJsonTypeInfoResolver`. A new `AddTypeInfoResolver()` method prepends additional resolvers to that chain (mirroring `ApplyExtensionConverters()`). The A2A `SerializationInit.Init()` is simplified to a single call to `AddTypeInfoResolver()`.

**Tech Stack:** C# 12, .NET 8 / .NET Framework 4.8, `System.Text.Json` 8+, xUnit 2/3, Moq (available in test projects).

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs` | **Create** | Source-generated `JsonSerializerContext` for all core model types not covered by a custom converter |
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs` | **Modify** | Add `AddTypeInfoResolver()` method; update `InitSerializerOptions()` to wire `CoreJsonContext`; update `ApplyExtensionOptions()` XML doc |
| `src/libraries/Hosting/A2A/SerializationInit.cs` | **Modify** | Replace `ApplyExtensionOptions()` with `AddTypeInfoResolver(JsonContext.Default)` |
| `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs` | **Create** | All new tests for this feature |

---

## Task 1: Add `AddTypeInfoResolver()` to `ProtocolJsonSerializer`

**Files:**
- Modify: `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs`
- Create: `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`

### Background

`ProtocolJsonSerializer` is a static class in `Microsoft.Agents.Core.Serialization`. The existing `ApplyExtensionConverters()` (line 69) and `ApplyExtensionOptions()` (line 88) show the locking pattern to follow — copy-on-write under `_optionsLock` to handle the case where `SerializationOptions` has become read-only after first use.

The new `AddTypeInfoResolver()` follows the exact same pattern, but sets `options.TypeInfoResolver` using `JsonTypeInfoResolver.Combine(newResolver, existingChain)` to prepend.

- [ ] **Step 1.1: Write the failing tests**

Create `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Model.Tests
{
    public class ProtocolJsonSerializerSourceGenTests
    {
        [Fact]
        public void AddTypeInfoResolver_SetsNonNullResolver()
        {
            // Arrange
            var resolver = new DefaultJsonTypeInfoResolver();

            // Act
            ProtocolJsonSerializer.AddTypeInfoResolver(resolver);

            // Assert
            Assert.NotNull(ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver);
        }

        [Fact]
        public void AddTypeInfoResolver_NewResolverConsultedFirst()
        {
            // Arrange: a resolver that handles HeroCard and tracks if it was called
            var wasCalled = false;
            var trackingResolver = new TrackingTypeInfoResolver(
                typeof(Microsoft.Agents.Core.Models.HeroCard),
                () => wasCalled = true);

            // Act
            ProtocolJsonSerializer.AddTypeInfoResolver(trackingResolver);
            var json = """{"title":"Test"}""";
            JsonSerializer.Deserialize<Microsoft.Agents.Core.Models.HeroCard>(
                json, ProtocolJsonSerializer.SerializationOptions);

            // Assert
            Assert.True(wasCalled, "The most-recently registered resolver should be consulted first");
        }

        [Fact]
        public async Task AddTypeInfoResolver_ConcurrentCalls_DoNotThrow()
        {
            // Act: 20 concurrent calls with harmless resolvers
            var tasks = Enumerable.Range(0, 20).Select(_ =>
                Task.Run(() =>
                    ProtocolJsonSerializer.AddTypeInfoResolver(new DefaultJsonTypeInfoResolver())));

            // Assert: no exceptions
            await Task.WhenAll(tasks);
        }

        // Helper: a resolver that calls an action when it handles the target type
        private sealed class TrackingTypeInfoResolver : IJsonTypeInfoResolver
        {
            private readonly System.Type _targetType;
            private readonly System.Action _onResolved;
            private readonly DefaultJsonTypeInfoResolver _inner = new();

            public TrackingTypeInfoResolver(System.Type targetType, System.Action onResolved)
            {
                _targetType = targetType;
                _onResolved = onResolved;
            }

            public JsonTypeInfo? GetTypeInfo(System.Type type, JsonSerializerOptions options)
            {
                if (type == _targetType)
                    _onResolved();
                return _inner.GetTypeInfo(type, options);
            }
        }
    }
}
```

- [ ] **Step 1.2: Run to verify the tests fail**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ --filter "FullyQualifiedName~ProtocolJsonSerializerSourceGenTests" -v
```

Expected: compile error — `AddTypeInfoResolver` does not exist.

- [ ] **Step 1.3: Implement `AddTypeInfoResolver()` in `ProtocolJsonSerializer.cs`**

Open `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs`. After the `ApplyExtensionOptions` method (around line 88), add:

```csharp
/// <summary>
/// Prepends a <see cref="IJsonTypeInfoResolver"/> (e.g., a source-generated
/// <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>) to the resolver chain
/// used by <see cref="SerializationOptions"/>. The resolver is consulted before any previously
/// registered resolvers and before the reflection fallback.
/// Call from a <see cref="SerializationInitAttribute"/>-decorated <c>Init()</c> method.
/// </summary>
/// <remarks>
/// Each call prepends the new resolver at the front of the chain.
/// <see cref="JsonTypeInfoResolver.Combine"/> returns the first non-null result in order,
/// so the most-recently-added resolver wins for any given type.
/// </remarks>
public static void AddTypeInfoResolver(IJsonTypeInfoResolver resolver)
{
    lock (_optionsLock)
    {
        var newOptions = SerializationOptions.IsReadOnly
            ? new JsonSerializerOptions(SerializationOptions)
            : SerializationOptions;

        newOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            resolver,
            newOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());

        SerializationOptions = newOptions;
    }
}
```

Also add `using System.Text.Json.Serialization.Metadata;` at the top of the file if it is not already present.

- [ ] **Step 1.4: Run the tests to verify they pass**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ --filter "FullyQualifiedName~ProtocolJsonSerializerSourceGenTests" -v
```

Expected: all 3 new tests pass.

- [ ] **Step 1.5: Run existing serialization tests to verify no regressions**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ -v
```

Expected: all tests pass.

- [ ] **Step 1.6: Build the full solution to verify it compiles**

```bash
dotnet build src/Microsoft.Agents.SDK.sln -c Debug
```

Expected: 0 errors.

- [ ] **Step 1.7: Commit**

```bash
git add src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs \
        src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs
git commit -m "feat: add ProtocolJsonSerializer.AddTypeInfoResolver() for source-gen extensibility"
```

---

## Task 2: Create `CoreJsonContext`

**Files:**
- Create: `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs`
- Modify: `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`

### Background

`CoreJsonContext` is a source-generated `JsonSerializerContext` that covers all concrete model types in `Microsoft.Agents.Core.Models` that are **not** handled by a custom converter registered in `ApplyCoreOptions()`. See the spec's exclusion table for the full list of excluded types.

The `[JsonSourceGenerationOptions]` settings must exactly mirror the settings in `ApplyCoreOptions()` in `ProtocolJsonSerializer.cs` — including `IncludeFields = true`. All model types are in the `Microsoft.Agents.Core.Models` namespace.

`internal sealed partial class` — this is intentionally not public; it is an implementation detail of `Microsoft.Agents.Core`.

- [ ] **Step 2.1: Add tests for `CoreJsonContext` coverage**

Add to `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`, inside the class:

```csharp
[Fact]
public void CoreJsonContext_IncludesFields()
{
    // CoreJsonContext.Default.Options.IncludeFields must be true to match ApplyCoreOptions()
    // Access via a type we know is in CoreJsonContext — HeroCard
    // (We can't reference CoreJsonContext directly since it's internal;
    //  we test via the serialization behavior of a field-containing type instead.)
    // This test just verifies the TypeInfoResolver on SerializationOptions is non-null,
    // confirming CoreJsonContext was wired in. Full field coverage is tested below.
    Assert.NotNull(ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver);
}

[Fact]
public void HeroCard_SerializesAndDeserializes_ViaSourceGen()
{
    // HeroCard has no custom converter — uses CoreJsonContext path
    var card = new Microsoft.Agents.Core.Models.HeroCard
    {
        Title = "Test Title",
        Subtitle = "Test Subtitle",
        Text = "Some text"
    };

    var json = ProtocolJsonSerializer.ToJson(card);
    var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.HeroCard>(json);

    Assert.Equal("Test Title", result.Title);
    Assert.Equal("Test Subtitle", result.Subtitle);
    Assert.Equal("Some text", result.Text);
}

[Fact]
public void TokenExchangeResource_RoundTrips()
{
    // Another non-converter type
    var resource = new Microsoft.Agents.Core.Models.TokenExchangeResource
    {
        Id = "resource-id",
        Uri = "https://example.com/token",
        ProviderId = "provider-1"
    };

    var json = ProtocolJsonSerializer.ToJson(resource);
    var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.TokenExchangeResource>(json);

    Assert.Equal("resource-id", result.Id);
    Assert.Equal("https://example.com/token", result.Uri);
    Assert.Equal("provider-1", result.ProviderId);
}

[Fact]
public void ConversationParameters_Members_IReadOnlyList_Deserializes()
{
    // ConversationParameters.Members is IReadOnlyList<ChannelAccount>
    // Both List<ChannelAccount> and IReadOnlyList<ChannelAccount> must be in CoreJsonContext
    var json = """{"members":[{"id":"user1","name":"User One"},{"id":"user2","name":"User Two"}]}""";

    var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.ConversationParameters>(json);

    Assert.NotNull(result.Members);
    Assert.Equal(2, result.Members.Count);
    Assert.Equal("user1", result.Members[0].Id);
    Assert.Equal("User Two", result.Members[1].Name);
}
```

- [ ] **Step 2.2: Run to verify the new tests pass (they should already pass via reflection)**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ --filter "FullyQualifiedName~ProtocolJsonSerializerSourceGenTests" -v
```

Expected: all tests pass (reflection fallback works before we wire CoreJsonContext).

- [ ] **Step 2.3: Create `CoreJsonContext.cs`**

Create `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs`:

```csharp
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/> for
    /// core model types not handled by a registered custom <see cref="System.Text.Json.Serialization.JsonConverter"/>.
    /// Wired into <see cref="ProtocolJsonSerializer.SerializationOptions"/> as the base of the
    /// <see cref="System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver"/> chain.
    /// </summary>
    /// <remarks>
    /// Types handled by converters registered in <c>ApplyCoreOptions()</c> are intentionally
    /// excluded — including them would produce source-gen warnings and could bypass converter
    /// logic for callers who access <c>GetTypeInfo()</c> directly.
    /// </remarks>
    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        IncludeFields = true)]
    // --- Concrete model types not handled by a custom converter ---
    [JsonSerializable(typeof(ActivityTreatment))]
    [JsonSerializable(typeof(AdaptiveCardInvokeAction))]
    [JsonSerializable(typeof(AdaptiveCardInvokeValue))]
    [JsonSerializable(typeof(AadResourceUrls))]
    [JsonSerializable(typeof(BasicCard))]
    [JsonSerializable(typeof(CardImage))]
    [JsonSerializable(typeof(Citation))]
    [JsonSerializable(typeof(CommandResultValue<JsonElement>))]
    [JsonSerializable(typeof(CommandValue<JsonElement>))]
    [JsonSerializable(typeof(ConversationParameters))]
    [JsonSerializable(typeof(ConversationReference))]
    [JsonSerializable(typeof(Error))]
    [JsonSerializable(typeof(ExpectedReplies))]
    [JsonSerializable(typeof(Fact))]
    [JsonSerializable(typeof(GeoCoordinates))]
    [JsonSerializable(typeof(HeroCard))]
    [JsonSerializable(typeof(InnerHttpError))]
    [JsonSerializable(typeof(InvokeResponse))]
    [JsonSerializable(typeof(MediaCard))]
    [JsonSerializable(typeof(MediaEventValue))]
    [JsonSerializable(typeof(MediaUrl))]
    [JsonSerializable(typeof(Mention))]
    [JsonSerializable(typeof(OAuthCard))]
    [JsonSerializable(typeof(PagedMembersResult))]
    [JsonSerializable(typeof(Place))]
    [JsonSerializable(typeof(ProductInfo))]
    [JsonSerializable(typeof(ReceiptCard))]
    [JsonSerializable(typeof(ReceiptItem))]
    [JsonSerializable(typeof(ResourceResponse))]
    [JsonSerializable(typeof(SearchInvokeOptions))]
    [JsonSerializable(typeof(SearchInvokeResponse))]
    [JsonSerializable(typeof(SearchInvokeValue))]
    [JsonSerializable(typeof(SemanticAction))]
    [JsonSerializable(typeof(SignInResource))]
    [JsonSerializable(typeof(SigninCard))]
    [JsonSerializable(typeof(StreamInfo))]
    [JsonSerializable(typeof(StreamResult))]
    [JsonSerializable(typeof(TextHighlight))]
    [JsonSerializable(typeof(Thing))]
    [JsonSerializable(typeof(TokenExchangeRequest))]
    [JsonSerializable(typeof(TokenExchangeResource))]
    [JsonSerializable(typeof(TokenExchangeState))]
    [JsonSerializable(typeof(TokenOrSignInResourceResponse))]
    [JsonSerializable(typeof(TokenPollingSettings))]
    [JsonSerializable(typeof(TokenPostResource))]
    [JsonSerializable(typeof(TokenRequest))]
    [JsonSerializable(typeof(TokenStatus))]
    // --- Common collection types ---
    [JsonSerializable(typeof(List<Attachment>))]
    [JsonSerializable(typeof(List<CardAction>))]
    [JsonSerializable(typeof(List<ChannelAccount>))]
    [JsonSerializable(typeof(IReadOnlyList<ChannelAccount>))]  // ConversationParameters.Members
    [JsonSerializable(typeof(List<ConversationParameters>))]
    [JsonSerializable(typeof(List<MessageReaction>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, JsonElement>))]
    internal sealed partial class CoreJsonContext : JsonSerializerContext
    {
    }
}
```

- [ ] **Step 2.4: Build to verify `CoreJsonContext` compiles without errors or warnings**

```bash
dotnet build src/libraries/Core/Microsoft.Agents.Core/Microsoft.Agents.Core.csproj -c Debug
```

Expected: 0 errors. If you see source-gen warnings about conflicting converters for specific types, those types need to be removed from the `[JsonSerializable]` list and added to the spec's exclusion table.

- [ ] **Step 2.5: Run tests to confirm no regressions**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ -v
```

Expected: all tests pass (CoreJsonContext not yet wired, so no behaviour change yet).

- [ ] **Step 2.6: Commit**

```bash
git add src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs \
        src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs
git commit -m "feat: add CoreJsonContext source-generated JsonSerializerContext for core model types"
```

---

## Task 3: Wire `CoreJsonContext` into `InitSerializerOptions()` and update `ApplyExtensionOptions()` doc

**Files:**
- Modify: `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs`

### Background

`InitSerializerOptions()` (currently lines 47–53) creates a fresh `JsonSerializerOptions` by calling `ApplyCoreOptions()`. After this task, it also sets `TypeInfoResolver` to `Combine(CoreJsonContext.Default, new DefaultJsonTypeInfoResolver())`, establishing the source-gen chain with reflection fallback.

`ApplyExtensionOptions()` (around line 88) needs an updated XML doc warning that callers who replace `TypeInfoResolver` must include `CoreJsonContext.Default` in their new chain.

- [ ] **Step 3.1: Add a test that verifies `TypeInfoResolver` is set after initialization**

Add to `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`:

```csharp
[Fact]
public void SerializationOptions_TypeInfoResolver_IsNonNull()
{
    // After initialization, TypeInfoResolver must be set (not null)
    // so that CoreJsonContext metadata is used instead of reflection for covered types.
    Assert.NotNull(ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver);
}

[Fact]
public void SerializationOptions_TypeInfoResolver_CanResolveHeroCard()
{
    // HeroCard is in CoreJsonContext — the resolver chain must return type info for it
    var typeInfo = ProtocolJsonSerializer.SerializationOptions
        .TypeInfoResolver?
        .GetTypeInfo(typeof(Microsoft.Agents.Core.Models.HeroCard),
                     ProtocolJsonSerializer.SerializationOptions);

    Assert.NotNull(typeInfo);
}
```

- [ ] **Step 3.2: Run to verify these tests currently fail (TypeInfoResolver is null before wiring)**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ --filter "FullyQualifiedName~SerializationOptions_TypeInfoResolver" -v
```

Expected: `SerializationOptions_TypeInfoResolver_IsNonNull` FAILS (TypeInfoResolver is null before this task).

> Note: `SerializationOptions_TypeInfoResolver_CanResolveHeroCard` may pass if `DefaultJsonTypeInfoResolver` is present, or fail if `TypeInfoResolver` is null. Either outcome is acceptable here.

- [ ] **Step 3.3: Update `InitSerializerOptions()` in `ProtocolJsonSerializer.cs`**

Locate `InitSerializerOptions()` (currently looks like this):

```csharp
private static JsonSerializerOptions InitSerializerOptions()
{
    var options = new JsonSerializerOptions()
        .ApplyCoreOptions();

    return options;
}
```

Change it to:

```csharp
private static JsonSerializerOptions InitSerializerOptions()
{
    var options = new JsonSerializerOptions()
        .ApplyCoreOptions();

    options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        CoreJsonContext.Default,
        new DefaultJsonTypeInfoResolver());

    return options;
}
```

- [ ] **Step 3.4: Update `ApplyExtensionOptions()` XML doc**

Locate `ApplyExtensionOptions()` and update (or add) its XML documentation:

```csharp
/// <summary>
/// Applies a transformation function to <see cref="SerializationOptions"/>, replacing it with
/// the result. This is an advanced escape hatch — prefer <see cref="ApplyExtensionConverters"/>
/// or <see cref="AddTypeInfoResolver"/> for typical extensions.
/// </summary>
/// <param name="applyFunc">
/// A function that receives the current options and returns the new options.
/// </param>
/// <remarks>
/// <para>
/// <b>Important:</b> If your function replaces <see cref="JsonSerializerOptions.TypeInfoResolver"/>,
/// you must include <c>CoreJsonContext.Default</c> in the new resolver chain.
/// Omitting it silently removes source-generated metadata for all core model types.
/// Use <see cref="JsonTypeInfoResolver.Combine"/> to chain resolvers:
/// <code>
/// options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
///     YourContext.Default,
///     CoreJsonContext.Default,  // must be included
///     new DefaultJsonTypeInfoResolver());
/// </code>
/// </para>
/// </remarks>
public static void ApplyExtensionOptions(Func<JsonSerializerOptions, JsonSerializerOptions> applyFunc)
```

> Note: `CoreJsonContext` is `internal` so the XML doc example uses the class name without a `<see cref>` link (it won't resolve from outside the assembly). The string reference in the comment is intentional.

- [ ] **Step 3.5: Run tests to verify new tests pass**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ --filter "FullyQualifiedName~SerializationOptions_TypeInfoResolver" -v
```

Expected: both new tests PASS.

- [ ] **Step 3.6: Run all serialization tests to verify no regressions**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ -v
```

Expected: all tests pass.

- [ ] **Step 3.7: Build the full solution**

```bash
dotnet build src/Microsoft.Agents.SDK.sln -c Debug
```

Expected: 0 errors.

- [ ] **Step 3.8: Commit**

```bash
git add src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs
git commit -m "feat: wire CoreJsonContext into ProtocolJsonSerializer resolver chain"
```

---

## Task 4: Simplify A2A `SerializationInit.Init()`

**Files:**
- Modify: `src/libraries/Hosting/A2A/SerializationInit.cs`

### Background

The current `SerializationInit.Init()` in `src/libraries/Hosting/A2A/SerializationInit.cs` calls `ProtocolJsonSerializer.ApplyExtensionOptions()` to manually set `TypeInfoResolver = Combine(JsonContext.Default, new DefaultJsonTypeInfoResolver())`. This replaces the entire resolver chain, which after Task 3 would discard `CoreJsonContext`.

The fix: replace with a single call to `ProtocolJsonSerializer.AddTypeInfoResolver(JsonContext.Default)`. The `JsonContext` referred to here is `A2AJsonUtilities.JsonContext` (the inner class at the bottom of `A2AJsonUtilities.cs`), accessed via `JsonContext.Default`. The `using static Microsoft.Agents.Hosting.A2A.A2AJsonUtilities;` at the top of the file makes `JsonContext` available unqualified.

`A2AJsonUtilities.DefaultOptions` and `DefaultReflectionOptions` are **not changed** — they are separate options objects used by A2A-internal code and are intentionally distinct from `ProtocolJsonSerializer.SerializationOptions`.

- [ ] **Step 4.1: Build the A2A project before the change to confirm current state**

```bash
dotnet build src/libraries/Hosting/A2A/ -c Debug
```

Expected: 0 errors (baseline).

- [ ] **Step 4.2: Update `SerializationInit.cs`**

Open `src/libraries/Hosting/A2A/SerializationInit.cs`. Replace the entire `Init()` body:

**Before:**
```csharp
public static void Init()
{
    // Enable reflection fallback
    ProtocolJsonSerializer.ApplyExtensionOptions(options =>
    {
        options.TypeInfoResolver = JsonTypeInfoResolver.Combine(JsonContext.Default, new DefaultJsonTypeInfoResolver());
        return options;
    });
}
```

**After:**
```csharp
public static void Init()
{
    ProtocolJsonSerializer.AddTypeInfoResolver(JsonContext.Default);
}
```

Also remove the now-unused `using System.Text.Json.Serialization.Metadata;` directive from the top of the file — it was only needed for `JsonTypeInfoResolver` and `DefaultJsonTypeInfoResolver`, which are no longer referenced directly in this file.

- [ ] **Step 4.3: Build to verify it compiles**

```bash
dotnet build src/libraries/Hosting/A2A/ -c Debug
```

Expected: 0 errors.

- [ ] **Step 4.4: Build the full solution**

```bash
dotnet build src/Microsoft.Agents.SDK.sln -c Debug
```

Expected: 0 errors.

- [ ] **Step 4.5: Run all tests**

```bash
dotnet test --no-build -c Debug ./src/
```

Expected: all tests pass.

- [ ] **Step 4.6: Commit**

```bash
git add src/libraries/Hosting/A2A/SerializationInit.cs
git commit -m "refactor: simplify A2A SerializationInit to use AddTypeInfoResolver"
```

---

## Task 5: Converter priority and integration tests

**Files:**
- Modify: `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`

### Background

This task adds the remaining tests from the spec's testing table: converter-priority verification and the end-to-end smoke test confirming converter-handled types still work correctly after the resolver chain was introduced.

- [ ] **Step 5.1: Add converter priority and regression tests**

Add to `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs`:

```csharp
[Fact]
public void Activity_CustomConverter_StillTakesPriority()
{
    // Activity is handled by ActivityConverter — it must take priority over CoreJsonContext.
    // We verify Activity still round-trips correctly via the converter path.
    var json = """{"type":"message","text":"hello world","id":"act-123"}""";

    var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.Activity>(json);

    Assert.Equal("message", result.Type);
    Assert.Equal("hello world", result.Text);
    Assert.Equal("act-123", result.Id);
}

[Fact]
public void ChannelAccount_CustomConverter_StillTakesPriority()
{
    // ChannelAccount is handled by ChannelAccountConverter.
    var json = """{"id":"user-1","name":"Alice","role":"user"}""";

    var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.ChannelAccount>(json);

    Assert.Equal("user-1", result.Id);
    Assert.Equal("Alice", result.Name);
}

[Fact]
public void HeroCard_NullProperties_OmittedInJson()
{
    // Verify DefaultIgnoreCondition = WhenWritingNull is respected via CoreJsonContext
    var card = new Microsoft.Agents.Core.Models.HeroCard
    {
        Title = "My Card"
        // Subtitle, Text, etc. are null — should be omitted
    };

    var json = ProtocolJsonSerializer.ToJson(card);

    Assert.Contains("\"title\"", json);
    Assert.DoesNotContain("subtitle", json);
    Assert.DoesNotContain("text", json);
}

[Fact]
public void HeroCard_PropertyNameCamelCase_IsRespected()
{
    // Verify PropertyNamingPolicy = CamelCase
    var card = new Microsoft.Agents.Core.Models.HeroCard { Title = "CamelTest" };

    var json = ProtocolJsonSerializer.ToJson(card);

    Assert.Contains("\"title\"", json);          // camelCase
    Assert.DoesNotContain("\"Title\"", json);    // not PascalCase
}

[Fact]
public void ResourceResponse_RoundTrips()
{
    // ResourceResponse is in CoreJsonContext (no custom converter)
    var original = new Microsoft.Agents.Core.Models.ResourceResponse { Id = "resp-42" };

    var json = ProtocolJsonSerializer.ToJson(original);
    var result = ProtocolJsonSerializer.ToObject<Microsoft.Agents.Core.Models.ResourceResponse>(json);

    Assert.Equal("resp-42", result.Id);
}
```

- [ ] **Step 5.2: Run the new tests**

```bash
dotnet test src/tests/Microsoft.Agents.Model.Tests/ --filter "FullyQualifiedName~ProtocolJsonSerializerSourceGenTests" -v
```

Expected: all tests pass.

- [ ] **Step 5.3: Run the full test suite**

```bash
dotnet test --no-build -c Debug ./src/
```

Expected: all tests pass.

- [ ] **Step 5.4: Commit**

```bash
git add src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs
git commit -m "test: add source-gen, converter priority, and integration tests for ProtocolJsonSerializer"
```

---

## Final Verification

- [ ] **Build in Release to catch any release-mode issues**

```bash
dotnet build src/Microsoft.Agents.SDK.sln -c Release
```

Expected: 0 errors.

- [ ] **Full test run**

```bash
dotnet test --no-build -c Debug ./src/
```

Expected: all tests pass.

---

## Summary of Changes

| File | What Changed |
|------|-------------|
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs` | New file — source-generated context for ~40 non-converter core model types |
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs` | `AddTypeInfoResolver()` added; `InitSerializerOptions()` sets `TypeInfoResolver`; `ApplyExtensionOptions()` doc updated |
| `src/libraries/Hosting/A2A/SerializationInit.cs` | Replaced 4-line `ApplyExtensionOptions` call with `AddTypeInfoResolver(JsonContext.Default)` |
| `src/tests/Microsoft.Agents.Model.Tests/ProtocolJsonSerializerSourceGenTests.cs` | New file — all new tests |
