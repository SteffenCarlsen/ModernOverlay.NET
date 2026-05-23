# Integration Boundary

ModernOverlay core is standalone and cooperative.

It does not implement:

- process injection;
- global hooks for target-process control;
- anti-cheat bypass;
- protected-process bypass;
- capture-protection bypass;
- DRM bypass;
- kernel-level integration.

## Allowed Model

The supported model is an external overlay window that tracks normal desktop HWNDs or a cooperative owned application that explicitly communicates with the overlay.

## Cooperative Package

`ModernOverlay.Integration` provides the first cooperative integration surface:

- `OverlayCommandMessage` and `OverlayDrawCommand` define a small command protocol.
- `NamedPipeOverlayCommandServer` and `NamedPipeOverlayCommandClient` move command messages over a local named pipe.
- `CooperativeOverlayCommandHost` owns resource realization outside the render callback and applies accepted commands from an overlay render handler.
- Supported command kinds are `Start`, `Stop`, `Update`, and `Clear`.
- The current draw command set covers transparent clears, solid or linear-gradient brushes, lines, arrows, outlined/filled rectangles, rounded rectangles, circles, ellipses, triangles, corner boxes, crosshairs, inline geometry paths, images from paths or encoded bytes, and text runs.
- `OverlayResourceDefinition` lets owned hosts define reusable brush, font, image, and geometry resources by id. Draw commands can reference those ids, and `ReleaseResourceIds` disposes cached remote resources explicitly.
- Resource definitions are accepted transactionally with command updates: rejected updates dispose newly realized resources and keep the previous command/resource state intact.
- `OverlayCommandPatch` lets owned hosts append, insert, replace, remove, or clear command ids without resending the full command list. Patch application is transactional: rejected patches leave the previous command/resource state intact.
- `NamedPipeOverlayCommandSecurity.RequireCommandToken(...)` adds an opt-in shared local command token. The client stamps `CommandToken`, the server rejects missing or invalid tokens before invoking the command handler, and accepted messages are passed to the handler with the token removed.
- `NamedPipeOverlayCommandSecurity.CurrentUserOnly(...)` creates named pipes with a Windows ACL for the current user and can also require a command token. `NamedPipeOverlayCommandSecurity.WithPipeSecurity(...)` lets advanced owned hosts provide a custom `PipeSecurity` descriptor.
- `NamedPipeOverlayCommandServer` accepts multiple owned senders concurrently. The default limit is four active connections; callers can lower or raise it with `maxConcurrentConnections`.

This package is for owned/cooperative processes. It does not search for targets, inject into targets, install hooks, or bypass target restrictions.

The command token is a cooperation guard for local demos and owned tools. It helps prevent accidental or casual local senders from controlling the overlay pipe, but it is not a hostile-process security boundary, a sandbox, or an anti-cheat bypass mechanism.

Windows pipe ACLs are available for owned hosts that need a stronger local OS boundary than a shared token. The built-in current-user ACL is a practical default for same-user tools, while custom `PipeSecurity` descriptors let callers choose service, group, or account-specific access rules. ACLs still do not make the integration package a hostile-process security boundary; they only restrict which Windows principals can open the pipe.

Minimal server:

```csharp
using var host = new CooperativeOverlayCommandHost(overlay.Resources);
var server = new NamedPipeOverlayCommandServer(
    "modern-overlay-demo",
    (message, _) => ValueTask.FromResult(host.Handle(message)),
    NamedPipeOverlayCommandSecurity.CurrentUserOnly("local-token"));

await server.RunAsync(cancellationToken);
```

Minimal client:

```csharp
var client = new NamedPipeOverlayCommandClient("modern-overlay-demo", commandToken: "local-token");
await client.SendAsync(OverlayCommandMessage.Update(
[
    OverlayDrawCommand.Clear(ColorRgba.Transparent),
    OverlayDrawCommand.TextRun("owned host", new PointF(24, 24), ColorRgba.White),
]));
```

## Payload Strategy

Alpha IPC messages stay as line-delimited JSON over local named pipes. That keeps the owned-host protocol inspectable and easy to debug while the integration package is still preview.

Current limits are enforced by `OverlayCommandLimits`:

1. Serialized command message: 8 MiB of JSON text.
2. Inline image payload: 4 MiB of encoded image bytes before JSON/base64 expansion.
3. Geometry path payload: 4096 path commands per inline or remote geometry.
4. Draw command payload: 4096 draw commands per message.
5. Resource definition payload: 1024 resource definitions per message.
6. Resource release payload: 1024 release ids per message.
7. Remote resource id: 128 characters.

Large or frequently reused image/geometry data should be sent once as a remote resource definition and referenced by id in later updates. Payloads above these limits should move to future side-channel file or shared-memory transport work rather than stretching the JSON pipe protocol.

## Future Work

Future cooperative integration work can add side-channel file payloads, shared-memory payloads, and richer multi-client ownership/conflict policies if real owned-host scenarios need them. The first Windows pipe ACL hardening pass is implemented through `NamedPipeOverlayCommandSecurity.CurrentUserOnly(...)` and custom `PipeSecurity` support.

## Experimental Package

`ModernOverlay.Integration.Experimental` provides opt-in research contracts for authorized sample hosts. It is source-only for alpha package publication and should stay unlisted/unpackaged until there is a real authorized experimental provider:

- `IOverlayIntegrationProvider`
- `IRenderBridge`
- `IOverlayCommandTransport`
- `NamedPipeOverlayCommandTransport`
- `IsolatedOverlayIntegrationProvider`

Experimental providers are isolated from the host through result-returning initialization and exception-safe shutdown. The interfaces exchange public command messages and draw commands; they do not expose internal native resources.

The core package remains useful without the cooperative or experimental packages.
