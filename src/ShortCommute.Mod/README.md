# ShortCommute.Mod

The whole mod (AssemblyName `SylvanGames.ShortCommute`). Three files:

| file | role |
|---|---|
| `ShortCommuteConfigurator.cs` | `[Context("Game")]` Bindito wiring: binds `CommuteOptimizer` and registers it as a `TemplateModule` decorator on every `DistrictCenter`. No Harmony. |
| `CommuteOptimizer.cs` | Per-district `TickableComponent`. On day start (`OnDaytimeStart`) it rebuilds its worker queue and the distinct-workplace target set, then each `Tick` drains the queue under a per-tick fill budget, moving/swapping homes via the greedy `TryImprove` / `FindSwapCandidate`. Reassigns `Home` only. To rank a worker's candidate homes it reads each dwelling row at that worker's workplace column and sorts. |
| `DwellingRowCache.cs` | The performance core. A `DwellingRow` is one dwelling's road distance to every relevant workplace (a dictionary keyed by workplace), built by a single **dwelling-rooted** flow-field fill and reused across the whole pass. `FillsThisTick` lets the component cap fresh fills per tick; `TryGetRow` returns `null` when the budget is spent so the caller defers. |

Flow: `Configure` → decorator attaches a `CommuteOptimizer` per district →
`OnDaytimeStart` enqueues employed adults, recomputes the workplace target set,
and clears the row cache → `Tick` processes workers under the budget, building
each dwelling's row (rooted at the dwelling) at most `MaxFillsPerTick` per tick,
ranking candidates from the cached rows, and applying the best move/swap. Swaps
are fill-free — their distances are column lookups in already-built rows.

See the repo `CLAUDE.md` for the load-bearing design facts (dwelling rooting,
fill budget, defer-on-budget, homes-only) and
`C:\Projects\TimberbornDevKit\docs\commute-balancer-analysis.md` for the full
rationale.
