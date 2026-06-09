# Commute overlay — design & handoff

Forward-looking plan for the **commute overlay**, the next major feature. The
data layer it depends on (`CommuteCost`) is in place; the overlay itself is not
started. This doc is the handoff: what's decided, what's still open, and the
sanity checks to run before writing rendering code.

> Read `CLAUDE.md` (load-bearing design facts) and `src/ShortCommute.Mod/README.md`
> first. This doc assumes that context.

## Where we are

- **Done:** `CommuteCost` (display-only `BaseComponent` on every `Worker`) is
  decorated *and bound* (`Bind<CommuteCost>().AsTransient()` — the binding was the
  fix in commit `a344511`; a decorated component with no binding throws
  `No binding exists` at world-load). The optimizer stamps it at every
  settle/move/swap exit via `RecordCommute` / `RecordPartnerCommute`. It's a free
  byproduct (distances it already has, no extra fills) and is **never read back by
  the algorithm**.
- **Not done:** the overlay UI that reads `CommuteCost`.

### Two inaccuracies the overlay must design around (both by design)

These are documented on `CommuteCost`'s XML doc and in `CLAUDE.md`. They are
acceptable *only because this is display*:

1. **Block-resolution.** Under clustering (`ClusterRadius = 10` road-tiles) a
   dwelling reports its cluster rep's distance, ±radius. Same-block houses report
   identical values. → The map view must use **colour bands wider than the
   radius** so the approximation never flips a colour. **Exact per-beaver numbers
   are not safe to read off the cache** — they'd be a visible white lie (identical
   numbers on adjacent houses). Reserve exact numbers for an on-select recompute.
2. **Last-rebalance-stale.** `CommuteCost` isn't reset at pass start (deliberately
   — avoids flicker to "no data"), so a value can lag up to a day. Fine for a
   heatmap; another reason exact numbers come from a fresh recompute, not the cache.

## The two viewing modes

1. **Browse / approximate — the map overlay.** Whole-map colour bands over
   dwellings (or beavers — see open decisions), drawn from the cached `CommuteCost`.
   Coarse, block-resolution, **no fills** — answers "where are my commute
   problems" at a glance. Grey = no data (the `CommuteCost.NoData` NaN sentinel:
   unemployed / no home / unreachable / not yet stamped).
2. **Inspect / exact — the on-select readout.** Select an individual beaver (or
   dwelling) and see its *true* commute, produced by a single **on-demand exact
   road-distance fill** for that beaver (one fill, off the tick path). Precise, so
   clustering's ±radius never reaches the number the player actually reads.

## UX: how the two modes are unlocked  *(RECOMMENDED — not yet user-confirmed)*

**Nested / progressive disclosure.** A single top-right toggle button is the one
entry point — think "commute analysis mode."

- **On:** the map is colour-banded **and** selecting any beaver/dwelling enriches
  its panel with the exact breakdown.
- **Off:** no overlay, no panel clutter, **and no on-select fills** — the exact
  recompute physically can't fire unless the overlay is on, so its (small) cost
  can never surprise the player.

This matches the workflow: flip on → scan for red → click the red house for real
numbers → fix. It is **not** game-progression-gated (it's an analysis tool, not a
gameplay unlock).

The fork still open for the user: **nested** (above) vs. **independent** (overlay
toggle and an always-on entity-panel commute stat are separate features — simpler
and more discoverable, but the exact fill then fires on *every* beaver selection
and the panel carries the stat permanently). I recommended nested; the user had
not confirmed before we moved to the binding fix. **Confirm before coding.**

## Rendering mechanism (researched)

ShortCommute ships **no asset bundle** (code-only mod). That rules out the
mesh-based approach and points at the asset-free highlight path:

- **Entity tint (use this):** `Timberborn.SelectionSystem.Highlighter.HighlightPrimary(BaseComponent, Color)`,
  driven via `RollingHighlighter` — the game's URP highlight render pass
  (`HighlightRenderingService`). No mesh/material asset needed.
- **Tile tint (if colouring ground instead of entities):**
  `Timberborn.…AreaHighlightingService.DrawTile(Vector3Int, Color)`.
- **NOT** `Timberborn.Rendering.MeshDrawer` — Keystone's biome overlay uses it, but
  it needs a mesh **asset**, which we don't have.
- **Toggle button:** `UILayout.AddTopRightButton(VisualElement, priority)` with a
  `Common/SquareToggle`. **Legend** (optional): auto-hide when toggled off.

### Copyable reference (read-only, do not edit)
Keystone's overlay is the pattern to crib the toggle + legend from (its *renderer*
uses MeshDrawer, which we replace with Highlighter):
- `C:\projects\TimberbornKeystone\src\Keystone.Mod\Visualization\BiomeOverlayRenderer.cs`
- `…\BiomeOverlayToggle.cs`
- `…\BiomeOverlayLegend.cs`

## Open decisions (settle before / during coding)

- **Surface to colour:** dwellings (recommended — `CommuteCost` is per-worker but a
  dwelling holds the workers; max-aggregate the occupants' costs so the worst
  commute drives the colour) vs. beavers directly.
- **Band thresholds:** absolute road-tile cutoffs (~25–30 tiles), **band width ≥
  ~2×ClusterRadius (≥ ~20 tiles)** so block-resolution error can't cross a band
  edge. Pick the ramp (e.g. green→yellow→orange→red) and exact cutoffs.
- **Mode 2 placement:** entity selection/info panel only, or also a hover tooltip?
- **Grey/no-data styling:** how "no data" reads vs. "short commute."

## Verification gaps (do these first)

1. **Reload DLLs in-game** (full save reload — `CommuteCost` populates during a
   rebalance pass). Until then the overlay would render all-grey because nothing
   has stamped a value yet. The currently-running game is still a pre-`CommuteCost`
   build.
2. **Fresh CSV capture** after reload to confirm `CommuteCost` adds **no measurable
   overhead** (analytically it's free — a lookup the optimizer already had — but
   that's unverified in-game). Profiler: TimberDevKit, probe on
   `SylvanGames.ShortCommute.CommuteOptimizer`. CSVs land under
   `…\Mods\TimberDevKit\profiles\shortcommute-*.csv`.
3. **Step-0 sanity check before building the renderer:** verify
   `Highlighter` / `RollingHighlighter` is DI-injectable into a singleton with no
   asset bundle present. If it isn't, fall back to `AreaHighlightingService.DrawTile`
   on the dwelling footprints.
