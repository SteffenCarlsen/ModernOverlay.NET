# ADR 0001: .NET 11 Preview Primary Target and .NET 10 Contingency

## Status

Accepted for bootstrap.

## Context

The modernization spec requires `net11.0-windows` as the primary target, with `net10.0-windows` as a stable contingency if .NET 11 preview or Vortice compatibility blocks implementation.

## Decision

The repository targets `net11.0-windows` on `main`. We will not multi-target during the first implementation pass.

## Contingency Trigger

Create a `net10.0-windows` fallback branch if any of these become true:

- Vortice packages fail to restore or compile on `net11.0-windows`.
- Direct2D/DirectWrite/WIC/DXGI smoke tests fail due to .NET 11 preview runtime or SDK issues.
- CI cannot install or run the .NET 11 preview SDK reliably.
- Packaging or analyzers produce preview-only blockers that do not exist on .NET 10.

## Consequences

This keeps the first code path simple while preserving an explicit fallback decision point with concrete failure evidence.
