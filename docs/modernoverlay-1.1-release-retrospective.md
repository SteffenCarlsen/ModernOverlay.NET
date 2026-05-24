# ModernOverlay 1.1 Release Retrospective

Date: 2026-05-24
Branch: `retrospective/Release-1.1`
Release: `v1.1.0`

## Scope

Release 1.1 added the `ModernOverlay.UI` package, retained interactive controls, selective click-through input regions, floating windows, popups, text editing, layout panels, diagnostics, samples, release packaging coverage, and a broad UI test suite.

The work moved from architecture planning through implementation, visual sample iteration, PR review triage, release workflow updates, and final publication to GitHub Releases and NuGet.

## Evidence Reviewed

- Git commit range from `752eedd` to merge commit `d6559ff`.
- `docs/modernoverlay-1.1-interactive-ui-tasks.md`.
- `docs/modernoverlay-1.1-ui-validation-20260524.md`.
- `docs/release-validation-checklist.md`.
- GitHub release `v1.1.0`, published on 2026-05-24.
- NuGet package index checks for all six published packages.
- Release workflow run `26368691328`, completed successfully.

## What Worked

1. **Explicit architecture decisions kept scope stable.**
   The work stayed focused on a generic overlay UI framework and avoided importing game-specific behavior from the PoC. Decisions like interface-only layout persistence, deferred `ScrollViewer`, and `ModernOverlay.UI` as its own package prevented avoidable coupling.

2. **The task document was a useful control surface.**
   Updating `docs/modernoverlay-1.1-interactive-ui-tasks.md` as decisions changed kept architecture, implementation, tests, and docs aligned. It also made late questions easier to resolve without rediscovering old context.

3. **The A/B sample exposed real interaction bugs.**
   Screenshots and manual sample use found issues that unit tests alone would not have made obvious: imprecise hit testing, caret positioning, slider boundary visuals, popup z-order, color picker ergonomics, window placement clamping, and misleading layout previews.

4. **Review triage improved correctness.**
   The PR review contained both valid Win32 risks and false positives. Inspecting the code path before agreeing led to a focused fix for `WindowProc` message handling while avoiding unnecessary changes for already-rooted delegates and collectible parent-child event cycles.

5. **Release preflight caught external-state risks.**
   Checking tags, GitHub releases, NuGet package versions, workflow package lists, and trusted publishing setup made `ModernOverlay.UI` publication a registry-backed fact rather than a local assumption.

## What Hurt

1. **Solution shape regressed late.**
   Adding projects through tooling reintroduced virtual solution folders and `NestedProjects`. CI caught it, but this is exactly the kind of mechanical repo rule that should stay in `Lessons.md` and release gates.

2. **Visual bugs often looked component-specific when they were shared.**
   Hit testing, text measurement, z-order, and window placement problems appeared in one control first, but the correct fix usually belonged in shared geometry, layout, or rendering code.

3. **The sample became the primary UX test harness after the fact.**
   `UiAbTestOverlay` became extremely useful, but only after several iterations added visible state, clearer layout previews, tooltip coverage, and control-specific affordances. Future UI work should design that sample role earlier.

4. **New package publication has two independent failure modes.**
   The repo can correctly build and pack a package while NuGet still refuses it if trusted publishing policy does not cover the new package.

## Actionable Lessons Added

The following lessons were promoted into `Lessons.md`:

- Release preflight must check external version availability before publishing.
- Package-consumer smoke tests should isolate restore output to avoid stale global NuGet cache hits.
- New packages require both workflow coverage and NuGet trusted publishing coverage.
- Screenshot-reported UI precision bugs should first be investigated in shared geometry and measurement paths.
- UI samples should be treated as validation tools, not just demonstrations.
- Text editing should share measured text advances across caret, selection, scrolling, and click placement.
- External review findings should be classified by evidence, not severity labels.
- Owned parent-child event cycles are not automatically leaks in .NET.
- Win32 message handlers should return safe native results and record diagnostics instead of throwing across `WindowProc`.

## Agents.md Updates

The retrospective added two behavioral rule groups:

1. **PR Review Follow-Up Rules**
   Require accepted/rejected/deferred classification, code evidence for rejections, small fixes for proven risks, and PR comments for non-trivial reviews.

2. **Release Readiness Rules**
   Require current evidence for release state, external version checks, package workflow checks, NuGet policy checks, and preservation of reusable release lessons.

## Skill Candidates

These are not repository code changes yet, but they are good candidates for future Codex skills if the workflow repeats.

1. **ModernOverlay release preflight**
   A skill that runs the exact release-readiness checks: branch state, solution shape, restore, build with package version, non-integration tests, pack package list, benchmark dry run, GitHub release/tag collision checks, NuGet version checks, and trusted publishing reminders.

2. **ModernOverlay UI visual triage**
   A skill for screenshot-driven UI debugging that starts from shared geometry, transform, measurement, z-layer, and focus/capture paths before patching individual controls.

3. **Truth-first PR review response**
   A skill that fetches PR comments, maps claims to code paths, classifies findings as accepted/rejected/deferred, applies supported fixes, verifies them, and posts a concise evidence-based follow-up comment.

## Follow-Up Backlog

1. Add `FontOptions` support for font weight/style, already tracked in the 1.1 task backlog.
2. Consider splitting `TextBox` into model, input, and rendering helpers after behavior stabilizes.
3. Consider a deliberate theme/defaults token pass instead of ad hoc constant extraction.
4. Add broader human visual validation on Windows 11, mixed-DPI monitor movement, fullscreen/borderless scenarios, and more GPU/driver combinations.
5. Revisit `ScrollViewer` only after transformed coordinate handling, clipping, wheel routing, popup placement, and virtualization boundaries are designed.
