# HostedActivityService Options Design

## Goal

Decouple `HostedActivityService` shutdown configuration from adapter-specific
configuration without introducing per-turn scope behavior from PR #960.

## Configuration

Add a public `HostedActivityServiceOptions` class in the ASP.NET Core hosting
library. Its constructor accepts `IConfiguration` and binds the
`HostedActivityServiceOptions` section from appsettings.

The class exposes `ShutdownTimeoutSeconds`, defaulting to 60 when the section or
property is absent.

```json
{
  "HostedActivityServiceOptions": {
    "ShutdownTimeoutSeconds": 60
  }
}
```

`AddAsyncAdapterSupport` registers `HostedActivityServiceOptions` as a singleton
using the application's `IConfiguration`.

## Constructor Compatibility

Retain the existing `HostedActivityService` constructor that accepts an optional
`AdapterOptions`. Mark the legacy constructor obsolete and delegate it to a new
constructor.

The new constructor accepts `HostedActivityServiceOptions` as its required fifth
parameter and retains an optional legacy `AdapterOptions` as its sixth parameter.
This shape preserves existing four- and five-argument source calls and the
existing constructor signature for binary compatibility, while allowing DI to
select the new constructor because `HostedActivityServiceOptions` is registered.

Shutdown timeout precedence is:

1. `HostedActivityServiceOptions.ShutdownTimeoutSeconds`
2. Legacy `AdapterOptions.ShutdownTimeoutSeconds`
3. The default value of 60

The new options therefore win whenever they are supplied.

## AdapterOptions Deprecation

Mark `AdapterOptions.ShutdownTimeoutSeconds` obsolete with the message:
`Use HostedActivityServiceOptions instead.`

Keep the property functional for compatibility. Narrowly suppress obsolete
warnings in internal compatibility paths that must continue reading it,
including `HostedTaskService` until it is migrated separately.

## Scope

This change only moves shutdown configuration ownership. It does not add
`UseScopePerTurn`, create dependency injection scopes, or modify
`ActivityWithClaims`.

## Tests

Add focused tests covering:

- Binding `ShutdownTimeoutSeconds` from the `HostedActivityServiceOptions`
  configuration section.
- The default value when configuration is absent.
- Registration and resolution through `AddAsyncAdapterSupport`.
- New options taking precedence over legacy `AdapterOptions`.
- Legacy constructor fallback behavior.
