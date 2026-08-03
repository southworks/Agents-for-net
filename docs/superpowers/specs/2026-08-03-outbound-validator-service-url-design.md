# Outbound Validator Service URL Design

## Goal

Remove `AdapterOptions.ValidateServiceUrl` while preserving claim-based `Activity.ServiceUrl`
validation under the existing `IOutboundHostValidator.Enabled` switch.

## Behavior

- When `IOutboundHostValidator.Enabled` is `true`, `CloudAdapter` rejects an activity with HTTP
  400 if its `ServiceUrl` is not allowed by the outbound host validator.
- When the validator is enabled and the inbound identity contains a `serviceurl` claim,
  `CloudAdapter` also rejects malformed URLs or a host mismatch between the claim and the
  activity.
- When the validator is disabled, allowlist enforcement is skipped. If a `serviceurl` claim is
  present and its host does not match the activity URL, `CloudAdapter` logs the existing warning
  and continues processing.
- A missing identity, missing claim, or missing activity `ServiceUrl` continues without
  claim-based rejection.

## Implementation

Delete `ValidateServiceUrl` from `AdapterOptions`. In `CloudAdapter.ValidateServiceUrl`, replace
the adapter-option enforcement condition with `_hostValidator.Enabled`. Keep the allowlist check
ahead of the claim comparison so enabled validation remains fail-closed for disallowed hosts even
when no claim is available.

Update the `OutboundHostValidatorOptions` documentation to remove the obsolete comparison to
`CloudAdapterOptions.ValidateServiceUrl`.

## Tests

Update `CloudAdapterTests` to construct enabled or disabled outbound validators instead of setting
`AdapterOptions.ValidateServiceUrl`. Cover matching hosts, mismatched hosts, missing claims,
missing activity URLs, invoke activities, malformed claim URLs, malformed activity URLs, and the
existing allowlist scenarios.

## Documentation

Update `microsoft-365-agents/secure-your-agent-dotnet.md` in the
`businessapps-copilot-docs-pr` repository:

- Remove `CloudAdapterOptions:ValidateServiceUrl` from the control table, configuration examples,
  and checklist.
- Explain that `OutboundHostValidator:Enabled` controls both the allowlist and claim-host
  validation.
- Preserve the distinction that claim mismatches warn but do not reject while the validator is
  disabled.
- Update `ms.date` to `08/03/2026`.
