# Alpha Release Artifact Manifest

Date: 2026-05-22
Version: `0.1.0-preview`
Validation source: `docs/release-validation-results-20260522-local.md`

This manifest records the local package set produced by the alpha command gate. It is not a publish record or a Git tag; it is the auditable package artifact state that would be used for a first MVP/alpha release.

## Package Set

| Package | Size | SHA-256 |
|---|---:|---|
| `ModernOverlay.0.1.0-preview.nupkg` | 76,417 bytes | `BF004F05D9B5C1F8BD469E80A5C95B1DB1688B0F341620720F7D94019A09D382` |
| `ModernOverlay.Diagnostics.0.1.0-preview.nupkg` | 9,290 bytes | `EDC3525E61D742B2843F8B61B59EF6B3548085B76ACD9313321B7C52F8C5B484` |
| `ModernOverlay.Direct2D.0.1.0-preview.nupkg` | 20,926 bytes | `72185B46035DE21C765ADFD75E44B1A3466EEAF459D75EA35AB973683D6EF374` |
| `ModernOverlay.Integration.0.1.0-preview.nupkg` | 33,192 bytes | `123E24E5963E56720EC91D68FA92ADF9B44CAADB9A89A1A907E6F13758AAFE93` |
| `ModernOverlay.Win32.0.1.0-preview.nupkg` | 27,750 bytes | `C5674BC9D35C4C93424267AC7EF7395A8FF74F544192FA87DB9A978D2C4DD749` |

## Package Boundary

1. `ModernOverlay.Integration.Experimental` is intentionally absent from the package set.
2. `ModernOverlay` includes the bundled common-path Direct2D backend entries:
   - `lib/net11.0-windows7.0/ModernOverlay.Direct2D.dll`
   - `lib/net11.0-windows7.0/ModernOverlay.Direct2D.xml`
3. All five alpha packages include `README.md`.
4. All five alpha packages include release notes with the required `net11.0-windows` and DWM/color-key transparency fallback caveats.

## Release Caveats

1. The package targets `net11.0-windows` on a .NET 11 preview SDK.
2. The API and package layout are preview surfaces, not 1.0-stable contracts.
3. `TransparencyMode.UpdateLayeredWindow` and `TransparencyMode.DirectComposition` currently fall back to the DWM/color-key Direct2D HWND path with diagnostics.
4. Windows 11, mixed-DPI, fullscreen/borderless, and additional GPU/driver validation remain follow-up hardening checks.
5. This is an MVP/alpha artifact set for experimentation, not a production-readiness claim.
