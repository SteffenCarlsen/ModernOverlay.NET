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

## Future Packages

`ModernOverlay.Integration` may later provide named-pipe or shared-memory command transport for owned/cooperative applications.

`ModernOverlay.Integration.Experimental` may later provide research extension points for authorized sample hosts.

Those packages must remain isolated and opt-in. The core package must remain useful without them.

