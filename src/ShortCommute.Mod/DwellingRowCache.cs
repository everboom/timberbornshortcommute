using System.Collections.Generic;
using Timberborn.DwellingSystem;
using Timberborn.Navigation;
using Timberborn.WorkSystem;

namespace SylvanGames.ShortCommute {

  /// <summary>
  /// One dwelling's road distance to every relevant workplace, produced by a
  /// single exploration rooted at that dwelling. <see cref="Distances"/> is
  /// keyed by workplace, so "how far is this dwelling from job X?" is an O(1)
  /// lookup.
  /// </summary>
  internal sealed class DwellingRow {

    public readonly Dictionary<Workplace, float> Distances;

    public DwellingRow(Dictionary<Workplace, float> distances) => Distances = distances;

  }

  /// <summary>
  /// Caches <see cref="DwellingRow"/>s for the current rebalance pass under a
  /// hard per-tick measurement budget.
  ///
  /// <para><b>We root explorations at the dwelling.</b> A road-path query is a
  /// Dijkstra flow-field fill rooted at the start node; rooting at the dwelling
  /// means one fill yields that dwelling's distance to <em>every</em> workplace
  /// at once. Two reasons this beats rooting at workplaces:</para>
  /// <list type="number">
  ///   <item>The dwelling count is floored by dwelling capacity, so it can't
  ///   run away — whereas a colony full of 1–2-worker buildings makes the
  ///   distinct-workplace count balloon toward the population.</item>
  ///   <item>It makes swap evaluation <b>fill-free</b>: the four distances a
  ///   swap needs are all column lookups in two rows we already built (the
  ///   mover's home and the target dwelling), no matter where the displaced
  ///   beavers happen to work.</item>
  /// </list>
  ///
  /// <para><b>Two lifetimes, composed.</b> Cached rows live for the whole pass
  /// (<see cref="Clear"/> at day start); the fill counter lives for one tick
  /// (<see cref="ResetTickCounter"/>). <see cref="TryGetRow"/> serves a cache
  /// hit for free, builds a miss if under budget, and returns <c>null</c> when
  /// a fresh fill is needed but the tick's budget is spent — telling the caller
  /// to defer and resume later, with the fills already made still cached.</para>
  /// </summary>
  internal sealed class DwellingRowCache {

    private readonly Dictionary<Dwelling, DwellingRow> _rows = new();
    private readonly int _maxFillsPerTick;

    public DwellingRowCache(int maxFillsPerTick) => _maxFillsPerTick = maxFillsPerTick;

    /// <summary>Fresh fills performed since the last <see cref="ResetTickCounter"/>.</summary>
    public int FillsThisTick { get; private set; }

    /// <summary>Reset the per-tick fill counter (call at the start of each tick).</summary>
    public void ResetTickCounter() => FillsThisTick = 0;

    /// <summary>Drop all cached rows (call at the start of each rebalance pass).</summary>
    public void Clear() => _rows.Clear();

    /// <summary>
    /// Return the dwelling's row. A cache hit is free; a miss builds it (one
    /// fill) only if the per-tick budget allows, otherwise returns <c>null</c>
    /// so the caller can defer to a later tick.
    /// </summary>
    public DwellingRow? TryGetRow(Dwelling dwelling, IReadOnlyList<Workplace> workplaces) {
      if (_rows.TryGetValue(dwelling, out var row)) {
        return row;
      }
      if (FillsThisTick >= _maxFillsPerTick) {
        return null; // budget spent this tick — caller defers; rows already built stay cached
      }
      row = Build(dwelling, workplaces);
      _rows[dwelling] = row;
      FillsThisTick++;
      return row;
    }

    private static DwellingRow Build(Dwelling dwelling, IReadOnlyList<Workplace> workplaces) {
      var distances = new Dictionary<Workplace, float>(workplaces.Count);
      var dwellingAccessible = dwelling.GetEnabledComponent<Accessible>();
      if (dwellingAccessible != null) {
        for (var i = 0; i < workplaces.Count; i++) {
          var workplace = workplaces[i];
          var workplaceAccessible = workplace.GetEnabledComponent<Accessible>();
          if (workplaceAccessible == null) {
            continue;
          }
          // Root at the dwelling: all workplaces share this one flow-field fill.
          if (dwellingAccessible.FindRoadPath(workplaceAccessible, out var distance)) {
            distances[workplace] = distance;
          } else if (workplaceAccessible == dwellingAccessible) {
            // Workplace and dwelling share an access point (e.g. a live-at-work building).
            distances[workplace] = 0f;
          }
        }
      }
      return new DwellingRow(distances);
    }

  }

}
