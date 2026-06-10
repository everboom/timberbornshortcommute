# Overlay

The **commute overlay** — the in-game visualization that reads the display-only
`CommuteCost` the main optimizer stamps. One top-right toggle ("commute analysis
mode"); while on, what's drawn is context-sensitive to the current selection.
Full design and the verified game APIs it stands on are in
`docs/commute-overlay-plan.md` — read it first.

| file | role |
|---|---|
| `CommuteOverlayConfigurator.cs` | `[Context("Game")]` Bindito wiring: binds the overlay singletons. No decorators (the overlay only *reads* `CommuteCost`). |
| `CommuteOverlayToggle.cs` | `ILoadableSingleton`. The top-right `Common/SquareToggle` button; exposes `Enabled`. Custom icon **without** an asset bundle: the embedded PNG, decoded by `EmbeddedTexture`, is painted onto the toggle checkmark's `backgroundImage`. Plain-text tooltip backs it up. |
| `EmbeddedTexture.cs` | Decodes a PNG embedded in the assembly into a runtime `Texture2D` (main-thread only; loud on a missing/undecodable resource). The bundle-free way to ship a UI icon. |
| `Icons/CommuteOverlayIcon.png` | The toggle icon (52×52 RGBA), embedded via `<EmbeddedResource>` with an explicit `LogicalName`. |
| `CommuteOverlayRenderer.cs` | `ILoadableSingleton` + `IUpdatableSingleton`. The brain: per-frame heatmap refresh while enabled, and selection-driven line/path draws via the `EventBus` selection events. Reads `CommuteCost`; never mutates game state. Sets `CommuteOverlaySuppression.Active`, mirrors the opt-in setting into `.HidePathRange`, and flips `CommuteDataService.StampingEnabled` so the (core) frame-loop gatherer stamps `CommuteCost` only while the overlay is showing. |
| `CommuteLineDrawer.cs` | Pooled Unity `LineRenderer`s with a runtime URP/Unlit material per band colour. The one rendering primitive with no in-game precedent (the spike); fails loudly if the URP shader is missing. |
| `CommuteBands.cs` | Maps a road distance to a discrete band `Color` (or `null` = no data). Cutoffs are placeholder pending a real CSV capture. |
| `CommuteOverlayPatcher.cs` | `ILoadableSingleton`. Applies the mod's **only** Harmony patches: three flag-gated prefixes. Two suppress vanilla selection highlights (`DistanceHeatmapShower.ShowHeatmap`, `MechanicalGraphHighlightService.HighlightSelectedNode`) while the overlay is on. The third suppresses the vanilla path-range mesh (`DistrictPathNavRangeDrawer.LateUpdate` — a heavy per-frame rebuild) when the overlay is on **and** the player opt-in is set. Inert when the overlay is off. |
| `CommuteOverlaySuppression.cs` | Static `Active` + `HidePathRange` flags the patcher's prefixes read; written by the renderer. |
| `CommuteOverlaySettings.cs` | `ModSettingsOwner` (eMka.ModSettings). One toggle, `HidePathRangeOverlay` (**on** by default), shown in the game's Mods settings panel. Literal text, no loc file. Only applies while the overlay is active, so it's vanilla-neutral when the overlay is off. |
| `CommuteOverlaySettingsConfigurator.cs` | `[Context("MainMenu")][Context("Game")]` — binds the settings owner in both scopes so the panel finds it from either, without dragging the Game-only overlay singletons into MainMenu. |

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
  materials, the game's own highlight render pass, and the toggle icon decoded at
  runtime from a PNG embedded in the assembly (`EmbeddedTexture`) rather than
  loaded from a bundle.
- **Harmony is surgical and flag-gated.** Three prefixes, suppression only, inert
  while the overlay is off. The path-range one is additionally behind a player
  opt-in (the vanilla mesh it hides is a useful feature, just a heavy one). Don't
  broaden the patch surface; prefer a Bindito hook wherever one exists (the rest of
  the mod uses no Harmony at all).
- **`eMka.ModSettings` is the overlay's one dependency.** Only for the in-game
  toggle. Compile-only reference via `$(ModSettingsDir)`; declared as a `RequiredMod`
  in `manifest.json` so the player's installed mod provides the runtime DLLs.
