# Overlay

The **commute overlay** — the in-game visualization that reads the display-only
`CommuteCost` the main optimizer stamps. One top-right toggle ("commute analysis
mode"); while on, what's drawn is context-sensitive to the current selection.
Full design and the verified game APIs it stands on are in
`docs/commute-overlay-plan.md` — read it first.

| file | role |
|---|---|
| `CommuteOverlayConfigurator.cs` | `[Context("Game")]` Bindito wiring: binds the overlay singletons. No decorators (the overlay only *reads* `CommuteCost`). |
| `CommuteOverlayToggle.cs` | `ILoadableSingleton`. The top-right `Common/SquareToggle` button; exposes `Enabled`. No custom icon (we ship no asset bundle) — vanilla checkmark + plain-text tooltip. |
| `CommuteOverlayRenderer.cs` | `ILoadableSingleton` + `IUpdatableSingleton`. The brain: per-frame heatmap refresh while enabled, and selection-driven line/path draws via the `EventBus` selection events. Reads `CommuteCost`; never mutates game state. Sets `CommuteOverlaySuppression.Active`. |
| `CommuteLineDrawer.cs` | Pooled Unity `LineRenderer`s with a runtime URP/Unlit material per band colour. The one rendering primitive with no in-game precedent (the spike); fails loudly if the URP shader is missing. |
| `CommuteBands.cs` | Maps a road distance to a discrete band `Color` (or `null` = no data). Cutoffs are placeholder pending a real CSV capture. |
| `CommuteOverlayPatcher.cs` | `ILoadableSingleton`. Applies the mod's **only** Harmony patches: two flag-gated prefixes that suppress vanilla selection highlights (`DistanceHeatmapShower.ShowHeatmap`, `MechanicalGraphHighlightService.HighlightSelectedNode`) while the overlay is on, so they don't overwrite/clutter the overlay's own secondary highlights. Inert when the overlay is off. |
| `CommuteOverlaySuppression.cs` | Static `Active` flag the patcher's prefixes read; written by the renderer. |

## How it draws (by selection)

- **Nothing** → heatmap: every dwelling secondary-highlighted by its *worst*
  occupant's commute band (`Highlighter.HighlightSecondary`, isolated from the
  selection-highlight layer). No-data houses are left un-highlighted.
- **House** → straight lines to each occupant's workplace.
- **Workplace** → straight lines to each assigned worker's home.
- **Beaver** → the walked path home→workplace (`Accessible.FindPathUnlimitedRange`,
  one pathfind, off the optimizer's tick path).

Lines are coloured per worker by the same band ramp. Turning the toggle off clears
all highlights and lines and prevents the path recompute from firing at all.

## Load-bearing facts (don't regress)

- **`HighlightSecondary`, not tile tint, for the heatmap** — tinting the whole
  house mesh is far more visible than a ground tile, and the secondary layer rides
  alongside selection (which owns the primary layer). Mirrors the vanilla
  power-network overlay.
- **Read-only of game state.** The overlay never reassigns homes or touches
  `CommuteCost` — it's a pure consumer of data the optimizer already produces. (Its
  Harmony patches only *suppress* vanilla highlights; they mutate no game state.)
- **No asset bundle.** Everything is code-only: vanilla UI prefabs, runtime
  materials, the game's own highlight render pass.
- **Harmony is surgical and flag-gated.** Two prefixes, suppression only, inert
  while the overlay is off. Don't broaden the patch surface; prefer a Bindito hook
  wherever one exists (the rest of the mod uses no Harmony at all).
