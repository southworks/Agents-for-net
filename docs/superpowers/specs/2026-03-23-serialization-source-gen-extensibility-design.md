# Serialization Source Generation & Third-Party Extensibility

**Date:** 2026-03-23
**Status:** Approved
**Scope:** `Microsoft.Agents.Core`, `Microsoft.Agents.Hosting.A2A`

---

## Problem Statement

`ProtocolJsonSerializer` is the central serialization hub for the Agents SDK. It currently relies entirely on reflection-based `System.Text.Json` serialization. Third-party extensibility exists via the `[SerializationInit]` attribute mechanism, which allows libraries to register additional `JsonConverter` instances at startup. However:

1. There is no source-generated `JsonSerializerContext` for core types, meaning no AOT/trimming support.
2. There is no formalized way for third-party libraries to register a source-generated `JsonSerializerContext` into the resolver chain.
3. The A2A extension works around this with a manual `ApplyExtensionOptions()` call that rebuilds the `TypeInfoResolver` from scratch — fragile and not composable.

---

## Goals

- Add a `CoreJsonContext : JsonSerializerContext` covering core model types **not already handled by a custom converter**.
- Wire `CoreJsonContext` into `ProtocolJsonSerializer.SerializationOptions` as the base of the resolver chain, with `DefaultJsonTypeInfoResolver` as a reflection fallback.
- Add `ProtocolJsonSerializer.AddTypeInfoResolver(IJsonTypeInfoResolver)` as the formalized extension point for source-generated contexts, symmetric with the existing `ApplyExtensionConverters()`.
- Simplify the A2A `SerializationInit.Init()` to use the new method.
- Document the complete pattern for third-party library authors.

---

## Architecture

### Resolver Chain

After startup with no third-party extensions:

```
CoreJsonContext.Default  →  DefaultJsonTypeInfoResolver
```

After a third-party (or first-party extension like A2A) calls `AddTypeInfoResolver()`:

```
[most-recently registered]  →  ...  →  CoreJsonContext.Default  →  DefaultJsonTypeInfoResolver
```

Each call to `AddTypeInfoResolver()` prepends the new resolver at the front of the chain. `JsonTypeInfoResolver.Combine()` returns the first non-null `JsonTypeInfo` in order, so the most-recently-added resolver wins for any given type.

Custom converters registered via `options.Converters` (including all existing core converters) are **independent** of this chain and take precedence for their handled types, regardless of resolver order.

### Custom Converter Scope and CoreJsonContext Exclusions

`System.Text.Json` checks `options.Converters` before consulting `TypeInfoResolver`. The following core types are handled by dedicated custom converters and are **excluded from `CoreJsonContext`**. The `DefaultJsonTypeInfoResolver` (reflection fallback) provides their metadata when the converter needs it:

| Type(s) | Converter |
|---------|-----------|
| `Activity`, `IActivity` | `ActivityConverter`, `IActivityConverter` |
| `AnimationCard` | `AnimationCardConverter` |
| `Attachment` | `AttachmentConverter` |
| `AudioCard` | `AudioCardConverter` |
| `CardAction` | `CardActionConverter` |
| `ChannelAccount` | `ChannelAccountConverter` |
| `ConversationAccount` | `ConversationAccountConverter` |
| `Entity`, `AIEntity` | `EntityConverter`, `AIEntityConverter` |
| `TokenExchangeInvokeRequest` | `TokenExchangeInvokeRequestConverter` |
| `TokenExchangeInvokeResponse` | `TokenExchangeInvokeResponseConverter` |
| `TokenResponse` | `TokenResponseConverter` |
| `VideoCard` | `VideoCardConverter` |
| `SuggestedActions` | `SuggestedActionsConverter` |
| `AdaptiveCardInvokeResponse` | `AdaptiveCardInvokeResponseConverter` |
| `MessageReaction` | `MessageReactionConverter` |
| `Array2D` (internal) | `Array2DConverter` |
| `Dictionary<string, object>` | `DictionaryOfObjectConverter` |

Including converter-handled types in `CoreJsonContext` would produce source-gen warnings, waste generated code, and create a semantic hazard for callers who access `CoreJsonContext.Default.GetTypeInfo(typeof(T))` directly — they would receive incomplete metadata that bypasses the converter's special behavior.

---

## Components

### 1. `CoreJsonContext` (new file)

**Location:** `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs`

**Access modifier:** `internal sealed partial class` — this context is an implementation detail of `Microsoft.Agents.Core`. It is intentionally not public; AOT tooling that needs to reference it should use `ProtocolJsonSerializer.SerializationOptions.TypeInfoResolver` directly.

A `partial class` decorated with `[JsonSourceGenerationOptions]` and one `[JsonSerializable]` per concrete model type not covered by a custom converter. Options mirror `ApplyCoreOptions()` exactly, including `IncludeFields = true`:

- `DefaultIgnoreCondition = WhenWritingNull`
- `PropertyNamingPolicy = CamelCase`
- `PropertyNameCaseInsensitive = true`
- `NumberHandling = AllowReadingFromString`
- `IncludeFields = true` — **required** to match `options.IncludeFields = true` set in `ApplyCoreOptions()`; omitting this would silently drop public fields on model types.

**Types covered** (types from the exclusion table above are omitted):

```csharp
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
    IncludeFields = true)]
// Concrete model types not handled by a custom converter
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
// Common collection types for the above
[JsonSerializable(typeof(List<Attachment>))]
[JsonSerializable(typeof(List<CardAction>))]
[JsonSerializable(typeof(List<ChannelAccount>))]
[JsonSerializable(typeof(IReadOnlyList<ChannelAccount>))]  // ConversationParameters.Members
[JsonSerializable(typeof(List<ConversationParameters>))]
[JsonSerializable(typeof(List<MessageReaction>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class CoreJsonContext : JsonSerializerContext { }
```

**Note on `ChannelId`:** `ChannelId` is serialized as its string representation by `ActivityConverter` (which handles the `ChannelId.SubChannel`/`ProductInfo` logic). Since `Activity` is excluded from `CoreJsonContext`, no separate `[JsonSerializable(typeof(ChannelId))]` entry is needed.

**Note on `IList<T>` and `IReadOnlyList<T>` properties:** Model types in `CoreJsonContext` may declare interface-typed collection properties. The `[JsonSerializable]` entries for concrete collection types ensure generated metadata is available for the runtime concrete values. `ConversationParameters.Members` is declared as `IReadOnlyList<ChannelAccount>` — both `List<ChannelAccount>` and `IReadOnlyList<ChannelAccount>` entries are included. `PagedMembersResult.Members` is declared as `IList<ChannelAccount>` — the existing `List<ChannelAccount>` entry covers the concrete runtime type; if AOT testing reveals that `IList<ChannelAccount>` also needs an explicit entry, add it during implementation. `ExpectedReplies.Activities` is `IList<IActivity>` — each `IActivity` element is handled at runtime by `IActivityConverter` from `options.Converters`, regardless of which context resolved the outer `ExpectedReplies` type; full AOT coverage for `IList<IActivity>` is deferred.

**Note on `object`-typed properties:** Several types in `CoreJsonContext` (including `SearchInvokeValue`, `ConversationParameters`, `ExpectedReplies`, `InvokeResponse`, `AdaptiveCardInvokeAction`, `MediaCard`, and others) have `object`-typed properties. When deserialized through the source-gen path these receive a `JsonElement`. This matches the current reflection-based behavior for types without a dedicated converter. For `SearchInvokeValue` specifically, `SearchInvokeValueConverter` exists in the codebase but is **not registered** in `ApplyCoreOptions()` and is therefore currently dead code — this is a pre-existing gap, out of scope for this spec.

**Note on `ConversationParameters.Agent` and nested converter-handled types:** When `CoreJsonContext` generates metadata for `ConversationParameters`, its `Agent: ChannelAccount` property is serialized by delegating to the serializer, which will use `ChannelAccountConverter` from `options.Converters`. Outer-type source-gen and inner-type custom converters compose correctly.

### 2. `ProtocolJsonSerializer.AddTypeInfoResolver()` (new method)

**Location:** `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs`

Prepends a resolver to the existing `TypeInfoResolver` chain under `_optionsLock`. Follows the same copy-on-write pattern as `ApplyExtensionConverters()`.

The `new JsonSerializerOptions(existingOptions)` copy constructor copies `TypeInfoResolver` by reference. Wrapping the existing combined resolver with `JsonTypeInfoResolver.Combine(newResolver, existingResolver)` creates a correctly nested chain. Repeated calls produce the expected prepend behavior without accumulating incorrect depth.

The `?? new DefaultJsonTypeInfoResolver()` guard is defensive programming for callers that bypassed `InitSerializerOptions()` (e.g., via `ApplyExtensionOptions()` or direct construction). Under normal operation, `TypeInfoResolver` is always non-null after `InitSerializerOptions()`.

```csharp
/// <summary>
/// Prepends a <see cref="IJsonTypeInfoResolver"/> (e.g. a source-generated
/// <see cref="JsonSerializerContext"/>) to the resolver chain used by
/// <see cref="SerializationOptions"/>. The resolver is consulted before
/// any previously registered resolvers and before the reflection fallback.
/// Call from a <see cref="SerializationInitAttribute"/>-decorated Init() method.
/// </summary>
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

### 3. `ProtocolJsonSerializer.InitSerializerOptions()` (modified)

Set `TypeInfoResolver` after applying core options:

```csharp
private static JsonSerializerOptions InitSerializerOptions()
{
    var options = new JsonSerializerOptions().ApplyCoreOptions();
    options.TypeInfoResolver = JsonTypeInfoResolver.Combine(
        CoreJsonContext.Default,
        new DefaultJsonTypeInfoResolver());
    return options;
}
```

### 4. `ApplyExtensionOptions()` documentation update

`ApplyExtensionOptions()` remains an escape hatch but its XML doc must be updated to warn that callers who replace `TypeInfoResolver` entirely are responsible for including `CoreJsonContext.Default` in their chain. Failure to do so silently removes source-gen metadata for core types.

### 5. A2A `SerializationInit.Init()` (simplified)

**Location:** `src/libraries/Hosting/A2A/SerializationInit.cs`

```csharp
public static void Init()
{
    ProtocolJsonSerializer.AddTypeInfoResolver(JsonContext.Default);
}
```

Replaces the manual `ApplyExtensionOptions()` call that incorrectly reset the resolver chain (overwriting `CoreJsonContext` with only `A2AJsonContext + reflection`).

**Note on `A2AJsonUtilities.DefaultOptions` and `DefaultReflectionOptions`:** These static fields in `A2AJsonUtilities` are separate `JsonSerializerOptions` instances used by A2A-internal code for A2A protocol types (JSON-RPC, AgentCard, etc.). They are intentionally distinct from `ProtocolJsonSerializer.SerializationOptions` and are not affected by this change. `A2AJsonContext.Default` is registered with `ProtocolJsonSerializer` so that A2A types can be resolved when serialized through the main serializer; the internal A2A options objects remain for direct A2A protocol use.

---

## Third-Party Extension Pattern

Third-party libraries implement the existing `[SerializationInit]` pattern and call `AddTypeInfoResolver()` from `Init()`. **Important:** auto-invocation of `Init()` requires the consuming project to reference the `Microsoft.Agents.Core` Roslyn source generator (included in the `Microsoft.Agents.Core` NuGet package). Without the generator, `[assembly: SerializationInitAssemblyAttribute(...)]` is not emitted and `Init()` is never called; in that case the assembly attribute must be emitted manually.

```csharp
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    IncludeFields = true)]
[JsonSerializable(typeof(MyCustomType))]
internal sealed partial class MyExtensionJsonContext : JsonSerializerContext { }

[SerializationInit]
internal class SerializationInit
{
    public static void Init()
    {
        // Register source-generated context (for types without custom converters)
        ProtocolJsonSerializer.AddTypeInfoResolver(MyExtensionJsonContext.Default);

        // Register custom converters as before (for polymorphic/complex types)
        ProtocolJsonSerializer.ApplyExtensionConverters([new MyCustomTypeConverter()]);
    }
}
```

---

## Error Handling & Edge Cases

**Thread safety:** `AddTypeInfoResolver()` uses `_optionsLock` — same pattern as `ApplyExtensionConverters()`. No new concurrency risks.

**`IsReadOnly` guard:** Options are cloned before mutation if already read-only (i.e., after first use).

**Resolver ordering:** Each call to `AddTypeInfoResolver()` prepends the new resolver at the front of the chain. `JsonTypeInfoResolver.Combine()` returns the first non-null `JsonTypeInfo` in order, so the most-recently-added resolver wins for any given type. This is consistent with `ApplyExtensionConverters()` where the last-added converter wins.

**Null `TypeInfoResolver` guard:** `?? new DefaultJsonTypeInfoResolver()` in `AddTypeInfoResolver()` handles the edge case where options were constructed without going through `InitSerializerOptions()` (e.g., after an `ApplyExtensionOptions()` call that replaced `TypeInfoResolver`).

**`ApplyExtensionOptions()` contract:** Callers who use `ApplyExtensionOptions()` to replace `TypeInfoResolver` entirely must include `CoreJsonContext.Default` in their new chain. The method XML doc is updated to state this explicitly.

---

## Testing

| Test | Validates |
|------|-----------|
| `AddTypeInfoResolver` prepends before existing resolvers | Resolver chain ordering |
| Deserialize a type covered by `CoreJsonContext` with reflection disabled | Source-gen path end-to-end |
| Deserialize a type with a `public` field (not property) through `CoreJsonContext` | `IncludeFields = true` is in effect |
| Deserialize `ConversationParameters` with a `Members` array — verify `IReadOnlyList<ChannelAccount>` populated | `IReadOnlyList<T>` collection entry correct |
| Deserialize a type handled by a custom converter | Converter still takes priority over resolver |
| Concurrent `AddTypeInfoResolver()` calls | Thread safety |
| A2A `SerializationInit.Init()` compiles and runs | Simplified A2A pattern |
| Existing converter unit tests pass unchanged | Regression |

---

## Files Changed

| File | Change |
|------|--------|
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs` | **New** — source-generated context for non-converter core types |
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs` | **Modified** — `InitSerializerOptions()` wires `CoreJsonContext`; new `AddTypeInfoResolver()`; updated `ApplyExtensionOptions()` doc |
| `src/libraries/Hosting/A2A/SerializationInit.cs` | **Modified** — use `AddTypeInfoResolver()` |
