# CLAUDE.md

Guidance for Claude Code working in the **ShortCommute** repo.

## What this is

A Timberborn mod (C#) that keeps employed beavers housed close to their workplace
by road distance, reassigning **homes only, never jobs**, via direct moves and
home swaps. It's a perf-focused reimplementation of the commute-balancing idea —
the design and its rationale are recorded in
`C:\Projects\TimberbornDevKit\docs\commute-balancer-analysis.md` (read it before
changing the algorithm; it explains the flow-field cost model and why the
existing mods stall).

`README.md` is the authoritative overview. This file adds the Claude-specific
working rules.

> Global C# / working-style conventions come from `~/.claude/CLAUDE.md` and apply
> here: no unsolicited code changes, be direct, never suggest committing/pushing,
> XML docs on public members, `#region` blocks, a README per code folder.

## The load-bearing design facts (don't regress these)

- **Root explorations at the dwelling, and cache them per pass.** `Accessible.FindRoadPath`
  roots a Dijkstra flow field at the accessible you call it on, so
  `dwellingAccessible.FindRoadPath(workplaceAccessible, …)` makes one fill serve
  all workplaces. We key the cache by *dwelling* (`DwellingRowCache`): one fill
  per dwelling per pass, reused across every worker. Chosen over rooting at
  workplaces for two reasons — (1) the dwelling count is floored by dwelling
  capacity, so it can't balloon the way 1–2-worker buildings inflate the
  distinct-workplace count; (2) it makes **swap evaluation fill-free** (all four
  swap distances are column lookups in the already-built home and target rows).
  Road distance is symmetric, so the direction is lossless. NB the original
  mod's actual bug is not the rooting *direction* (it also roots at dwellings) —
  it's that it re-fills per worker with no cache; the cache + per-tick budget are
  the real fixes.
- **The fill is the only expensive operation.** Lookups against a built row are
  free. The fill budget is denominated in *fills* (`MaxFillsPerFrame`, in
  `CommuteDataService`), not workers. A worker whose row isn't built yet *defers*
  (re-queued untouched, resumed next tick) — so the budget is a hard ceiling, not
  a between-workers suggestion.
- **Gather off the tick, shuffle on it (don't merge them back).** Two clocks by
  responsibility: building distance rows and stamping `CommuteCost` is *read-only*,
  so `CommuteDataService` (an `IUpdatableSingleton`) drives `CommuteOptimizer.GatherData`
  on the **frame loop** — which runs even while paused, so the overlay populates
  right after a load, before the first tick. The home moves/swaps are *mutation*,
  so they stay in `Tick` (pause-gated and speed-scaled for free) and only ever
  *read* already-gathered rows (`DwellingRowCache.TryGetCachedRow` never fills).
  This split is safe because a row is geometry (dwelling→workplace distance),
  independent of occupancy, so a move never invalidates one. Don't move the
  mutation to Update (framerate-coupled, unpaused gameplay) or fold gathering back
  into the tick (re-blanks the overlay until a pass runs, and won't run while paused).
- **Homes only.** Never reassign `Workplace`. All mutation goes through vanilla
  `Dwelling.Assign/UnassignDweller`, so saves stay valid and the mod is
  removable.
- **District guard at the top of `TryImprove` (correctness, don't remove).**
  A worker is skipped unless `workplace.GetComponent<DistrictBuilding>()?.District
  == _districtCenter`. The worker queue (`_pending`) and workplace set are snapshots
  from day start; a mid-pass road edit can split the district and leave them stale,
  naming a worker whose job is now in another district. Assigning such a worker a
  home in *this* district would be an invalid cross-district pairing — a real
  failure, unlike a merely stale distance (which is harmless and self-heals next
  pass). The guard converts that failure into a skip. Distance staleness is
  tolerated; district staleness is not.
- **Harmony only as a surgical strike, never broad strokes; no sibling-mod
  integration.** The core hooks in cleanly via a Bindito `Configurator` +
  `TemplateModule.AddDecorator<DistrictCenter, CommuteOptimizer>` — no patching.
  Harmony *is* now used, but only where there's no clean extension point and only
  as narrow, flag-gated prefixes (`Overlay/CommuteOverlayPatcher.cs`): two that
  suppress vanilla selection highlights while the overlay is active
  (`DistanceHeatmapShower.ShowHeatmap`,
  `MechanicalGraphHighlightService.HighlightSelectedNode`, gated on
  `CommuteOverlaySuppression.Active`); and a third that suppresses the vanilla
  path-range mesh (`DistrictPathNavRangeDrawer.LateUpdate` — a heavy per-frame
  rebuild profiled at ~17 ms+ on large path networks), gated on `Active` **and**
  the player toggle `HidePathRange` (on by default — only ever applies while the
  overlay is active, so vanilla is untouched with the overlay off). `0Harmony.dll` is **bundled**
  into the mod folder (so Harmony is not a `RequiredMods` entry; deleting the folder
  removes the mod). Don't reach for Harmony when a Bindito/decorator hook exists,
  and don't broaden the patch surface (transpilers, behaviour rewrites) without
  being asked. We intentionally do **not** integrate BeaverGenders / faction
  building control (the user doesn't run them); don't add that surface without being asked.
- **One `RequiredMods` dependency: `eMka.ModSettings`** (the overlay's in-game
  settings toggle). It's referenced compile-only via `$(ModSettingsDir)` (set in
  `Directory.Build.local.props`); the player's installed ModSettings mod provides
  the runtime DLLs. The settings owner (`Overlay/CommuteOverlaySettings.cs`) is
  bound in a dedicated `[Context("MainMenu")][Context("Game")]` configurator so the
  MainMenu binding is scoped to just the owner (the main overlay configurator stays
  Game-only). This is the mod's only external dependency — don't add more without
  being asked.
- **`CommuteCost` is display-only and a free byproduct — don't let it leak into the
  algorithm.** A `CommuteCost` component is decorated onto every `Worker` and stamped
  by `CommuteOptimizer.GatherData` (on the frame loop, while the overlay is active)
  with the beaver's current home→workplace road distance (a lookup it already has —
  no extra fills). It exists purely for the commute overlay; the move/swap logic
  must never read it back. Two deliberate inaccuracies, both documented in its XML
  doc and acceptable *only because it's display*: (1) **block-resolution** — under
  clustering it's the cluster rep's distance (±`ClusterRadius`), so same-block houses
  report identical values; the overlay must use colour bands wider than the radius and
  reserve exact per-beaver numbers for an on-select recompute. (2) **frame-stale** —
  it tracks current homes but lags a move by a frame, and a beaver whose home row
  isn't built yet keeps its prior reading (no flicker to "no data"). Not persisted
  (recomputed on the frame loop), so saves carry nothing new.

## Build / deploy

- `dotnet build` — builds **and deploys** to `%USERPROFILE%\Documents\Timberborn\Mods\ShortCommute`.
- `dotnet build -p:ShortCommuteDeploy=false` — build only.
- Always Release (forced in `Directory.Build.props`). No tests yet (single Mod assembly).
- Per-machine path via `Directory.Build.local.props` (gitignored) /
  `SHORTCOMMUTE_TIMBERBORN_DIR` / `KEYSTONE_TIMBERBORN_DIR` / `-p:`.

## Validating performance

The point of the mod is a flat frame. To measure, use the TimberDevKit profiler
(`C:\Projects\TimberbornDevKit`): clone `CommuteBalancerProbe` to a probe pointed
at `SylvanGames.ShortCommute.CommuteOptimizer` (`Tick` + `TryImprove`) and confirm
per-tick ms stays low (cache-hit ticks ~sub-ms, fresh-fill ticks a few ms),
versus the ~9.5 s single spike the original produced.

## Next feature — commute overlay

The data layer (`CommuteCost`) is in; the **overlay UI that reads it is the active
next feature**. Full design, UX decisions, rendering-mechanism research, open
questions, and the verification gaps to clear first are in
`docs/commute-overlay-plan.md` — **read it before starting overlay work.**

## Roadmap (deferred from v1 — add only if measured necessary)

Event-driven scheduling (vs the daily full pass), finer road-change invalidation
(vs clearing the row cache each pass), adaptive rooting (root at whichever of
dwellings/workplaces is the smaller set), and a settings UI for the tuning
constants. The v1 levers (dwelling rooting + per-tick budget + block clustering)
already flatten the frame; the rest optimizes steady-state work frequency for very
large colonies. (Block clustering, once listed here, has shipped — `ClusterRadius`
in `CommuteOptimizer` / the clustering path in `DwellingRowCache`.)

## Don't lift the original's source

This is a clean reimplementation. The move/swap *algorithm* is the obvious greedy
and not ownable, but write it in our own structure (the `WorkplaceRowCache` is the
architectural difference) — don't transcribe the decompiled third-party code.
