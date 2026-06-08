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
  free. The per-tick budget is denominated in *fills* (`MaxFillsPerTick`), not
  workers; cache-hit workers flow freely. A worker that needs an unaffordable
  fill *defers* (re-queued untouched, resumed next tick) — so the budget is a
  hard ceiling, not a between-workers suggestion.
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
- **No Harmony, no sibling-mod integration.** Hooks in via a Bindito
  `Configurator` + `TemplateModule.AddDecorator<DistrictCenter, CommuteOptimizer>`.
  We intentionally do **not** integrate BeaverGenders / faction building control
  (the user doesn't run them); don't add that surface without being asked.

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

## Roadmap (deferred from v1 — add only if measured necessary)

Event-driven scheduling (vs the daily full pass), finer road-change invalidation
(vs clearing the row cache each pass), house clustering by road distance, and a
settings UI for the tuning constants. The v1 levers (workplace rooting + per-tick
budget) already flatten the frame; the rest optimizes steady-state work frequency
for very large colonies.

## Don't lift the original's source

This is a clean reimplementation. The move/swap *algorithm* is the obvious greedy
and not ownable, but write it in our own structure (the `WorkplaceRowCache` is the
architectural difference) — don't transcribe the decompiled third-party code.
