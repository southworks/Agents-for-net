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

- Add a `CoreJsonContext : JsonSerializerContext` covering all concrete core model types.
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
[last registered]  →  ...  →  CoreJsonContext.Default  →  DefaultJsonTypeInfoResolver
```

Custom converters registered via `options.Converters` (including all existing core converters) are **independent** of this chain and take precedence for their handled types, regardless of resolver order.

### Custom Converter Priority

`System.Text.Json` checks `options.Converters` before consulting `TypeInfoResolver`. The existing converters (`ActivityConverter`, `EntityConverter`, `ChannelAccountConverter`, etc.) continue to handle polymorphic and complex types. `CoreJsonContext` covers the remaining types not handled by converters and provides AOT metadata for all types.

---

## Components

### 1. `CoreJsonContext` (new file)

**Location:** `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs`

A `partial class` decorated with `[JsonSourceGenerationOptions]` and one `[JsonSerializable]` per concrete model type and commonly-used collection type. Options mirror those in `ApplyCoreOptions()`:

- `DefaultIgnoreCondition = WhenWritingNull`
- `PropertyNamingPolicy = CamelCase`
- `PropertyNameCaseInsensitive = true`
- `NumberHandling = AllowReadingFromString`

Types covered include all concrete classes in `Microsoft.Agents.Core.Models` (Activity, Attachment, CardAction, ChannelAccount, ConversationAccount, ConversationParameters, ConversationReference, all card types, all token types, all invoke types, Entity subclasses, etc.) plus common generic collection types (`List<Activity>`, `List<Attachment>`, `Dictionary<string, JsonElement>`, etc.).

```csharp
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Activity))]
// ... all core model types ...
internal sealed partial class CoreJsonContext : JsonSerializerContext { }
```

### 2. `ProtocolJsonSerializer.AddTypeInfoResolver()` (new method)

**Location:** `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs`

Prepends a resolver to the existing `TypeInfoResolver` chain under `_optionsLock`. Follows the same copy-on-write pattern as `ApplyExtensionConverters()`.

```csharp
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

### 4. A2A `SerializationInit.Init()` (simplified)

**Location:** `src/libraries/Hosting/A2A/SerializationInit.cs`

```csharp
public static void Init()
{
    ProtocolJsonSerializer.AddTypeInfoResolver(JsonContext.Default);
}
```

Replaces the manual `ApplyExtensionOptions()` call that incorrectly reset the resolver chain, discarding `CoreJsonContext`.

---

## Third-Party Extension Pattern

Third-party libraries that want source-gen support implement the existing `[SerializationInit]` pattern and call `AddTypeInfoResolver()` from `Init()`:

```csharp
[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
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

The `[SerializationInit]` source generator auto-emits `[assembly: SerializationInitAssemblyAttribute(typeof(SerializationInit))]`, so `Init()` is called automatically when `ProtocolJsonSerializer` first initializes.

---

## Error Handling & Edge Cases

**Thread safety:** `AddTypeInfoResolver()` uses `_optionsLock` — same pattern as `ApplyExtensionConverters()`. No new concurrency risks.

**`IsReadOnly` guard:** Options are cloned before mutation if already read-only (i.e., after first use).

**Resolver ordering:** `JsonTypeInfoResolver.Combine()` returns the first non-null `JsonTypeInfo`. The last call to `AddTypeInfoResolver()` wins for a given type. This is consistent with `ApplyExtensionConverters()` where the last-added converter wins.

**Null `TypeInfoResolver` guard:** `?? new DefaultJsonTypeInfoResolver()` in `AddTypeInfoResolver()` handles the edge case where options are constructed without going through `InitSerializerOptions()`.

**`ApplyExtensionOptions()` contract unchanged:** Callers using `ApplyExtensionOptions()` to replace `TypeInfoResolver` entirely take full responsibility for including `CoreJsonContext.Default` in their chain. The method remains a valid escape hatch.

---

## Testing

| Test | Validates |
|------|-----------|
| `AddTypeInfoResolver` prepends before existing resolvers | Resolver chain ordering |
| Deserialize a type with no custom converter, reflection disabled | `CoreJsonContext` source-gen path |
| Deserialize a type with a custom converter | Converter still takes priority over resolver |
| Concurrent `AddTypeInfoResolver()` calls | Thread safety |
| A2A `SerializationInit.Init()` compiles and runs | Simplified A2A pattern |
| Existing converter unit tests pass unchanged | Regression |

---

## Files Changed

| File | Change |
|------|--------|
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/CoreJsonContext.cs` | **New** — source-generated context for all core types |
| `src/libraries/Core/Microsoft.Agents.Core/Serialization/ProtocolJsonSerializer.cs` | **Modified** — `InitSerializerOptions()` wires `CoreJsonContext`; new `AddTypeInfoResolver()` method |
| `src/libraries/Hosting/A2A/SerializationInit.cs` | **Modified** — use `AddTypeInfoResolver()` |
