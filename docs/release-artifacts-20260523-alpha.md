# Alpha Release Artifact Manifest

Date: 2026-05-23
Version: `0.1.0-preview`
Validation source: `docs/release-validation-results-20260523-local.md`

This manifest records the local package set produced by the alpha command gate after capture-exclusion support, cooperative named-pipe ACL hardening, and the window namespace cleanup were added. It is not a publish record or a Git tag; it is the auditable package artifact state that would be used for a first MVP/alpha release.

## Package Set

| Package | Size | SHA-256 |
|---|---:|---|
| `ModernOverlay.0.1.0-preview.nupkg` | 79,495 bytes | `DB05796AABB52395FD3FA55F4565EC5CFCC9B31372A0A60A625A7D8029C0DCF1` |
| `ModernOverlay.Diagnostics.0.1.0-preview.nupkg` | 11,098 bytes | `5895B181D36E44846C10802033A22E7937128C94E534EC17CF4505A356ED7414` |
| `ModernOverlay.Direct2D.0.1.0-preview.nupkg` | 22,965 bytes | `16E64D10AE706DF7053FF922438AED0F0FBEDD2F145CC7A95B946C65A5A026FB` |
| `ModernOverlay.Integration.0.1.0-preview.nupkg` | 35,689 bytes | `793D9E9274541C75655F2E422FF2FA99657F48635F97B234A8302ACE4EF4DB00` |
| `ModernOverlay.Win32.0.1.0-preview.nupkg` | 31,085 bytes | `3AA62B86EEFBF8095E40CBA65C54172A6340517F9A9BD68A90ED343D5876036C` |

## Package Boundary

1. `ModernOverlay.Integration.Experimental` is intentionally absent from the package set.
2. `ModernOverlay` includes the bundled common-path Direct2D backend entries:
   - `lib/net11.0-windows7.0/ModernOverlay.Direct2D.dll`
   - `lib/net11.0-windows7.0/ModernOverlay.Direct2D.xml`
3. All five alpha packages include `README.md`.
4. All five alpha packages include release notes with the required `net11.0-windows` and DWM/color-key transparency fallback caveats.
5. A package-consumer smoke app restored the local `ModernOverlay` package, compiled the intended namespace imports, ran successfully, and confirmed the bundled Direct2D backend DLL reaches the consumer output.

## Release Caveats

1. The package targets `net11.0-windows` on a .NET 11 preview SDK.
2. The API and package layout are preview surfaces, not 1.0-stable contracts.
3. `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` currently fall back to the DWM/color-key Direct2D HWND path with diagnostics.
4. `OverlayWindowOptions.ExcludeFromCapture` is a capture-backed overlay prerequisite, not a finished capture backend.
5. `NamedPipeOverlayCommandSecurity.CurrentUserOnly(...)` and custom `PipeSecurity` support are local cooperative IPC hardening, not a hostile-process security boundary.
6. Window configuration and target descriptor APIs are now in `ModernOverlay.Windows`; this is still a preview API shape, so package consumers should expect naming and namespace feedback before 1.0.
7. Windows 11, mixed-DPI, fullscreen/borderless, and additional GPU/driver validation remain follow-up hardening checks.
8. This is an MVP/alpha artifact set for experimentation, not a production-readiness claim.
