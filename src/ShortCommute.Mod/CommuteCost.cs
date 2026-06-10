using Timberborn.BaseComponentSystem;

namespace SylvanGames.ShortCommute {

  /// <summary>
  /// Display-only record of a beaver's current work-path cost: the road distance
  /// from its home to its workplace. Stamped by
  /// <see cref="CommuteOptimizer.GatherData"/> — driven on the frame loop by
  /// <see cref="CommuteDataService"/> while the overlay is active — from the
  /// beaver's current home row. Attached to every entity that has a
  /// <see cref="Timberborn.WorkSystem.Worker"/> (via an
  /// <c>AddDecorator&lt;Worker, CommuteCost&gt;</c> in
  /// <see cref="ShortCommuteConfigurator"/>). The commute overlay reads it; the
  /// optimizer's own move/swap logic never does.
  ///
  /// <para><b>Block-resolution, by design.</b> With dwelling clustering enabled
  /// (<see cref="DwellingRowCache"/>), the value is the cluster representative's
  /// distance, so same-block neighbours report the same cost — off by at most the
  /// cluster radius. The overlay is meant to present this in coarse colour bands
  /// (band width well above the cluster radius), where the approximation is
  /// invisible. Do <b>not</b> surface it as an exact per-beaver number without an
  /// on-select recompute, or same-block houses will visibly show identical values
  /// that aren't truly identical.</para>
  ///
  /// <para><b>Staleness, by design.</b> While the overlay is active the value
  /// tracks current homes, refreshed each frame — but it lags a beaver's move by a
  /// frame (the move happens on the tick; the re-stamp on the next gather), and a
  /// beaver whose home row isn't explored yet keeps its previous reading rather
  /// than flickering to "no data". So treat it as "current as of the last frame",
  /// not a guaranteed instantaneous value.</para>
  ///
  /// <para>Not persisted — it is recomputed on the frame loop, so saves carry
  /// nothing new and the mod stays cleanly removable. (Because gathering runs even
  /// while paused, the overlay still populates right after a load, before any
  /// rebalance tick.)</para>
  /// </summary>
  public class CommuteCost : BaseComponent {

    #region State

    /// <summary>Sentinel for "no commute measured" — no home, an unreachable
    /// home→job pair, or a beaver the optimizer has not processed yet.</summary>
    public const float NoData = float.NaN;

    /// <summary>Road distance from the beaver's home to its workplace, or
    /// <see cref="NoData"/> when no value has been measured.</summary>
    public float RoadDistance { get; private set; } = NoData;

    /// <summary>True when <see cref="RoadDistance"/> holds a measured value.</summary>
    public bool HasData => !float.IsNaN(RoadDistance);

    #endregion

    #region Mutation

    /// <summary>Record a freshly measured home-to-workplace road distance.</summary>
    public void Set(float roadDistance) => RoadDistance = roadDistance;

    /// <summary>Mark the cost unknown (no home, unreachable, or unemployed).</summary>
    public void Clear() => RoadDistance = NoData;

    #endregion

  }

}
