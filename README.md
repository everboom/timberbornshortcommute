# ShortCommute

A Timberborn mod that keeps employed beavers housed close — by **road distance** —
to their workplace, by reassigning **homes** (never jobs). It's a
performance-focused reimplementation of the commute-balancing idea.

## Why it exists

The established commute-balancer mods do the right *thing* but pay a steep
per-action cost: they compute road distance by rooting a pathfind at **each
dwelling**, rebuilt for **every worker, every day**. Internally each of those is
a full Dijkstra "flow field" fill, so a single beaver's relocation can cost
~40–150 ms, and a day's rebalance is seconds of main-thread work — felt as a
start-of-day freeze or a trail of stutters.

ShortCommute keeps the same feature and the same move/swap logic, but:

1. **Explores once per dwelling and caches the result.** A road-path query is a
   Dijkstra "flow field" fill rooted at the start node, so we root at the
   **dwelling**: one fill yields that dwelling's distance to *every* workplace at
   once, reused for the whole pass. Distinct expensive fills drop from
   ~(workers × dwellings) to ~(number of dwellings). Rooting at dwellings rather
   than workplaces because (a) the dwelling count is floored by dwelling
   capacity, so it can't balloon the way a colony full of 1–2-worker buildings
   inflates the distinct-workplace count, and (b) it makes **swap evaluation
   fill-free** — a swap's four distances are all column lookups in two
   already-built rows (the mover's home and the target). Road distance is
   symmetric, so the rooting direction is lossless.
2. **Spreads work under a hard per-frame budget.** The distance fills run on the
   frame loop (`CommuteDataService`) at one fill per frame; the home moves run on
   the simulation tick and only *read* already-built rows, deferring any worker
   whose row isn't ready yet. No single drain-everything frame.

Net: per-action cost falls roughly three orders of magnitude (to cache-lookup
territory) and per-frame cost stays flat.

Splitting the read-only distance gathering (frame loop, runs even while paused)
from the home reassignment (simulation tick) also means the **commute overlay
populates right after a load** — before the first rebalance tick, and while the
game is still paused. The rows are geometry (place→place), independent of who
lives where, so a move never invalidates one; that's what makes the split safe.

## What it does (gameplay)

Each in-game day, for each district, it nudges employed beavers toward closer
homes: a direct move into a nearer dwelling with a free slot, or a home swap with
another beaver when that lowers the combined commute. It respects dwelling
capacity and district boundaries, and gets more aggressive (using child slots /
displacing children) only when the district is adult-overpopulated. Jobs are
never touched — only `Home` assignments — so beavers never drop work mid-shift.

Tuning (hardcoded in v1, in `CommuteOptimizer`): `MinMoveImprovement = 4`,
`MinSwapImprovement = 10`, `MaxFillsPerTick = 2`, `MaxWorkersPerTick = 64`,
`ClusterRadius = 10` (road-tiles; dwellings within this share one fill — 0 disables).

## Install / use

- **Disable any other commute-balancer mod.** Two mods both reassigning homes on
  `DistrictCenter` will fight.
- **Requires the `eMka.ModSettings` mod** (for the commute overlay's in-game
  settings toggle). Harmony is bundled, not a dependency. The core optimizer is
  still pure Bindito + vanilla navigation; the dependency is the overlay's only one.
- Build deploys to `%USERPROFILE%\Documents\Timberborn\Mods\ShortCommute`.

## Build

```pwsh
copy Directory.Build.local.props.example Directory.Build.local.props   # set your install path once
dotnet build                       # builds + deploys
dotnet build -p:ShortCommuteDeploy=false   # build only
```

`TimberbornInstallDir` resolves from `-p:` → `SHORTCOMMUTE_TIMBERBORN_DIR` /
`KEYSTONE_TIMBERBORN_DIR` → `Directory.Build.local.props`. Always Release.

## Status — v1

Ships the levers that kill the freeze (dwelling-rooted cached explorations +
hard per-frame fill budget + **block clustering**: dwellings within `ClusterRadius`
road-tiles share one fill) on top of a daily full pass. Each `Worker` also carries
a display-only `CommuteCost` (its current home→job road distance), stamped as a
free byproduct of the frame-loop gathering — the data source for the **commute
overlay** (see `docs/commute-overlay-plan.md`).

Deliberately **deferred** (not needed to flatten the frame, added only if profiling
shows steady-state cost matters): event-driven scheduling instead of a daily sweep,
finer road-change invalidation, adaptive rooting (root at whichever of
dwellings/workplaces is the smaller set), and a settings UI for the tuning knobs.

## Layout

```
ShortCommute.slnx
├── manifest.json                  mod manifest (Id SylvanGames.ShortCommute; requires eMka.ModSettings)
├── docs/commute-overlay-plan.md   design & handoff for the next feature (the overlay)
└── src/ShortCommute.Mod/
    ├── ShortCommuteConfigurator.cs  Bindito wiring — decorates DistrictCenter + Worker; binds the data service
    ├── CommuteOptimizer.cs          per-district component: GatherData (rows + CommuteCost, read-only) + Tick (move/swap)
    ├── CommuteDataService.cs        IUpdatableSingleton: drives GatherData each frame (even paused) under a shared fill budget
    ├── DwellingRowCache.cs          dwelling-rooted distance rows (+ block clustering); pure store, built by the gatherer
    └── CommuteCost.cs               display-only per-Worker road distance, for the overlay (not read by the algorithm)
```
