# Release Validation Checklist

This checklist captures manual evidence required before calling a release production-ready.

## Windowing

- Transparent overlay appears.
- Clear-to-transparent works.
- Click-through passes input to the target below.
- Interactive mode receives pointer input.
- Showing the overlay does not steal focus.
- Topmost mode behaves as expected.
- Follow-target z-order works best-effort and limitations are documented.

## Rendering

- Text renders crisply.
- Images render.
- Dashed shapes render.
- Geometry paths render.
- Clip and transform scopes behave correctly.
- Resize does not crash or leak native resources.
- Manual recreation does not crash and resources draw again afterward.

## Targeting

- Whole-window target tracking works.
- Client-area target tracking works.
- Target minimize/restore behavior works.
- Target lost/reacquired events fire.
- Restricted, elevated, protected, or fullscreen targets fail with documented limitations rather than bypass behavior.

## DPI and Monitors

- Per-monitor DPI changes resize the backend correctly.
- Mixed-DPI monitors behave acceptably.
- Negative-coordinate monitor layouts behave acceptably.

## Diagnostics

- Frame timing updates.
- Resource counts update.
- Target HWND/bounds diagnostics update.
- Native failure diagnostics show useful context.
- EventSource and logging adapter emit expected events.

