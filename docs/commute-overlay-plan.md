# Commute overlay — design & handoff

Design for the **commute overlay**, the active feature. The data layer it depends
on (`CommuteCost`) is in place; the overlay UI is being built on top. This doc is
the source of truth for *what* we're building and *why*, and records the verified
game APIs it stands on.

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
- **In progress:** the overlay UI that reads `CommuteCost`.

### Two inaccuracies the overlay designs around (both by design)

Documented on `CommuteCost`'s XML doc and in `CLAUDE.md`. Acceptable *only because
this is display*:

1. **Block-resolution.** Under clustering (`ClusterRadius = 10` road-tiles) a
   dwelling reports its cluster rep's distance, ±radius. Same-block houses report
   identical values. → The whole-map heatmap uses **colour bands wider than the
   radius** so the approximation never flips a band. Exact per-beaver numbers are
   not safe to read off the cache; they come from an on-select recompute.
2. **Last-rebalance-stale.** `CommuteCost` isn't reset at pass start (deliberately
   — avoids flicker to "no data"), so a value can lag up to a day. Fine for a
   heatmap; another reason exact numbers come from a fresh recompute, not the cache.

## The interaction model — one mode, context-sensitive by selection

A single top-right toggle ("commute analysis mode"). While it is **on**, what the
overlay draws depends on what is selected — progressive disclosure driven by the
game's own selection events, not extra buttons:

| Selection            | What it shows                                              | Primitive                                              | Cost            |
|----------------------|-----------------------------------------------------------|--------------------------------------------------------|-----------------|
| **Nothing**          | whole-map dwelling **heatmap** — each house coloured by its **worst** occupant's commute band | `Highlighter.HighlightSecondary` per dwelling          | none (cache lookups) |
| **House (Dwelling)** | coloured **lines** from the house to each workplace its occupants work at | `LineRenderer` (2-point, solid band colour)            | none (endpoints known) |
| **Workplace**        | coloured **lines** to the homes its workers live in       | `LineRenderer` (2-point)                               | none            |
| **Beaver (Worker)**  | the **walked path** from its home to its workplace        | `LineRenderer` polyline over `FindPathUnlimitedRange` corners | one fill, off the tick path |

When the toggle is **off**: no heatmap, no lines, and the on-select path recompute
physically can't fire — its (small) cost can never surprise the player. Not
game-progression-gated; it's an analysis tool.

### Suppressing the two vanilla selection highlights (Harmony, surgical)

Selecting a building normally fires two vanilla highlighters that both paint the
**same `Secondary` layer the overlay uses**, so they clutter — and can overwrite —
the overlay's own colours:

- **`DistanceHeatmapShower.ShowHeatmap`** — decorated on every building with a path
  range (`BlockObjectWithPathRangeSpec`); on select it `HighlightSecondary`s every
  building in the district coloured by road distance from the selected one. This is
  the "distances to all workplaces" heatmap seen when selecting a dwelling.
- **`MechanicalGraphHighlightService.HighlightSelectedNode`** — on selecting a
  powered building, walks the power graph and `HighlightSecondary`s every connected
  node.

Neither has a Bindito/decorator off-switch (both are `internal`, selection-event
driven), so they're suppressed with **two flag-gated Harmony prefixes**
(`Overlay/CommuteOverlayPatcher.cs`): each returns `false` (skips the original)
only while `CommuteOverlaySuppression.Active` is set — which the renderer sets for
exactly as long as the overlay is on. Off → the prefix returns `true` and vanilla
behaves normally. This is the mod's **only** use of Harmony, kept to the "surgical
strike, not broad strokes" bar: two named methods, suppression only, no behaviour
rewrite. `0Harmony.dll` is **bundled** in the mod folder (no external
`RequiredMods`; removable by deleting the folder).

Known minor edge: toggling the overlay *on while a building is already selected*
leaves that one selection's vanilla highlight painted (the prefix only stops future
calls) until the selection changes. The clean flow — toggle on, then select — is
unaffected.

### Why these primitives (verified against the decompiled game)

- **Heatmap = `HighlightSecondary`, not tile tint.** `Highlighter` exposes two
  independent layers — `HighlightPrimary` (used by the game's selection/hover via
  `RollingHighlighter`) and `HighlightSecondary`. The vanilla **power-network
  overlay** (`MechanicalSystemHighlighting`) tints connected buildings with
  `HighlightSecondary` — direct precedent for a persistent, multi-entity, non-
  selection overlay layer that rides *alongside* selection instead of fighting it.
  The render path (`HighlightRenderingService`, a URP `ScriptableRenderPass`)
  reuses each building's own `MeshRenderer`s (it toggles a "Selection" rendering-
  layer bit) — **no material or mesh asset from us.** Colour is an arbitrary
  `Color`. Tinting the whole house mesh is far more visible than a faint ground
  tile, which is why we do **not** use `AreaHighlightingService.DrawTile` here.
- **Every entity is highlightable.** `HighlightableObject` is decorated onto every
  `TemplateSpec` (`SelectionSystemConfigurator`), so dwellings *and* beavers carry
  it — no per-type guard needed.
- **`Highlighter` is bound transient.** Each subsystem injects its own instance and
  manages its own highlight set (that's how the power overlay does it); they all
  write to the shared per-entity `HighlightableObject`. So our overlay owns an
  **isolated `HighlightSecondary` layer** — clearing it (`UnhighlightAllSecondary`)
  never touches selection highlights.
- **Lines = `LineRenderer` (the one piece with no in-game precedent).** The game
  has **no `LineRenderer` anywhere**, no `Shader.Find`, and builds all materials
  from asset specs (`new Material(spec.SomeMaterial.Asset)`). Where it shows "this
  links to that" (the power network) it **tints entities rather than drawing
  lines.** So our lines are a code-only `LineRenderer` with a runtime
  `new Material(Shader.Find("Universal Render Pipeline/Unlit"))`, **one material
  per colour band**, solid-coloured per line (set `material.color`; avoid the
  vertex-colour gradient path, which URP/Unlit ignores). **Verified in-game: lines
  render and colour correctly.**
  - **Known limitation — no always-on-top.** Lines are depth-tested, so terrain
    and buildings occlude them. URP's Unlit shader **ignores the `_ZTest` material
    property** (setting `_ZTest = Always` + Overlay render queue had no effect
    in-game), so an overlay-style "draw over everything" line is *not* reachable
    through this material. The real options, both deferred pending a call: (a) `GL`
    immediate-mode drawing each frame with a depth-ignoring material (e.g.
    `Hidden/Internal-Colored`) — always-on-top but thin (1px) lines and a camera
    hook; (b) bundle a custom always-on-top shader — which means shipping an asset
    bundle, reversing the code-only stance. For now lines stay depth-tested.
- **Path geometry exists.** `Accessible.FindRoadPath(out distance)` returns only
  the scalar the optimizer uses. `Accessible.FindPathUnlimitedRange(Vector3 start,
  List<PathCorner> corners, out distance)` fills in geometry (`PathCorner.Position`,
  `Cost`, `DistanceToNext`, `Speed`). Caveat for honesty: the **cost** the mod
  optimizes (`FindRoadPath`, road-network distance) and the **geometry**
  `FindPathUnlimitedRange` returns (the actual walked route) are different methods
  and won't always be the same route — fine for a display path labelled with the
  road number, but don't imply they're identical.

### Endpoint / enumeration APIs (verified)

- Global dwelling list: `EntityComponentRegistry.GetEnabled<Dwelling>()`.
- Line endpoint world position: `BlockObject.WorldCenterGrounded`.
- Path start point: `Accessible.UnblockedSingleAccess` (Vector3?).
- Selection events: `SelectableObjectSelectedEvent` / `SelectableObjectUnselectedEvent`
  on the `EventBus`; dispatch via `GetComponent<Dwelling>() / <Workplace>() / <Worker>()`.
- Toggle button: `UILayout.AddTopRightButton(root, priority)` on
  `ShowPrimaryUIEvent`, with a `Common/SquareToggle` from `VisualElementLoader`
  (vanilla UI prefab — no asset bundle). **We ship no asset bundle**, so the toggle
  uses no custom icon texture (vanilla checkmark + tooltip) — Keystone's
  `BiomeOverlayToggle` loads a custom icon via its bundle; we can't and don't.

## Band colours (placeholder — tune from a real CSV capture)

Worst-occupant aggregate per house. Band width ≥ ~2×`ClusterRadius` (≥ ~20 tiles)
so block-resolution error can't cross a band edge. Starting ramp, to be tuned once
a capture shows typical commute scale:

```
green   <= 20      orange  40-60
yellow  20-40      red     > 60
grey / no highlight: NoData (unemployed / no home / unreachable / not yet stamped)
```

"No data" houses are simply left un-highlighted (no secondary colour) rather than
painted grey — keeps the map quiet and reserves colour for actual signal.

## Open decisions (settle during coding)

- **Band cutoffs** — the numbers above are guesses; set them from a profiler/CSV
  capture of real commute scale on a large map before calling the ramp final.
- **Line clutter** — a big workplace draws one line per worker home. Per-line band
  colour already makes the long/red ones pop; revisit if dense colonies look noisy
  (cap count, or only draw the worst N).
- **Exact on-select number** — the beaver path is drawn; whether to also surface
  the exact road-distance *number* in the entity panel (vs. only the drawn path)
  is a follow-up once the panel-extension API is scoped.

## Verification gaps (clear as we go)

1. **LineRenderer spike** — confirm a code-only coloured `LineRenderer` renders
   correctly under the game's URP setup. This gates the whole line/path tier; built
   first.
2. **Reload DLLs in-game** (full save reload — `CommuteCost` populates during a
   rebalance pass). Until a pass runs, the heatmap renders empty (nothing stamped).
3. **Fresh CSV capture** after reload to confirm `CommuteCost` adds no measurable
   overhead (analytically free; unverified in-game). Profiler: TimberDevKit, probe
   on `SylvanGames.ShortCommute.CommuteOptimizer`.

## Copyable reference (read-only, do not edit)

Keystone's overlay is the pattern to crib the toggle + legend from (its *renderer*
uses MeshDrawer, which we replace with Highlighter + LineRenderer):
- `C:\projects\TimberbornKeystone\src\Keystone.Mod\Visualization\BiomeOverlayToggle.cs`
- `…\BiomeOverlayLegend.cs`
- `…\PlateauHighlighter.cs` (the `AreaHighlightingService` per-frame commit pattern)
