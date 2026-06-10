using System.Collections.Generic;
using Timberborn.SingletonSystem;

namespace SylvanGames.ShortCommute {

  /// <summary>
  /// Drives the optimizers' <em>read-only</em> data gathering once per frame.
  /// As an <see cref="IUpdatableSingleton"/> it runs every frame <b>even while the
  /// game is paused</b> — unlike the <see cref="CommuteOptimizer"/> tick that
  /// performs the actual home moves. Each frame it asks every registered optimizer
  /// to build any missing distance rows (under a shared per-frame fill budget) and,
  /// while the overlay is showing, to stamp <see cref="CommuteCost"/> from them.
  ///
  /// <para><b>Why gathering lives off the tick.</b> A distance row is geometry —
  /// dwelling-&gt;workplace road distance — independent of who lives where, so
  /// building it is safe off the simulation clock. Doing it here lets the commute
  /// overlay populate immediately after a load (paused, before the first tick),
  /// and lets a daily shuffle find its rows already warm. The home reassignment,
  /// which <em>is</em> gameplay mutation, stays on the tick where pause-gating and
  /// speed-scaling apply for free.</para>
  /// </summary>
  public sealed class CommuteDataService : IUpdatableSingleton {

    #region Tuning

    /// <summary>Hard ceiling on fresh dwelling explorations (fills) per frame,
    /// shared across all districts. A fill is the one expensive operation, so this
    /// is deliberately <b>one</b>: frame-rate impact comes first, and frames are
    /// frequent enough that warming a whole colony one fill at a time still only
    /// takes about a second — an acceptable overlay spool-up. Once every row is
    /// built the loop is all cache checks and near-free.</summary>
    private const int MaxFillsPerFrame = 1;

    #endregion

    #region State

    private readonly List<CommuteOptimizer> _optimizers = new();

    /// <summary>Round-robin start index, advanced each frame so no district starves
    /// the shared fill budget when several are warming up at once.</summary>
    private int _rotationStart;

    /// <summary>Set by the overlay while it is active. When on, gathering also
    /// stamps <see cref="CommuteCost"/> each frame so the heatmap/line colours track
    /// current homes; when off, rows still build (the shuffle needs them) but the
    /// per-frame stamping is skipped.</summary>
    public bool StampingEnabled { get; set; }

    #endregion

    #region Registration

    /// <summary>Register a district optimizer (called as its building finishes).</summary>
    public void Register(CommuteOptimizer optimizer) {
      if (!_optimizers.Contains(optimizer)) {
        _optimizers.Add(optimizer);
      }
    }

    /// <summary>Drop a district optimizer (called as its building is removed).</summary>
    public void Unregister(CommuteOptimizer optimizer) => _optimizers.Remove(optimizer);

    #endregion

    #region Frame loop

    /// <inheritdoc />
    public void UpdateSingleton() {
      var count = _optimizers.Count;
      if (count == 0) {
        return;
      }
      if (_rotationStart >= count) {
        _rotationStart = 0;
      }

      var remaining = MaxFillsPerFrame;
      for (var k = 0; k < count; k++) {
        var optimizer = _optimizers[(_rotationStart + k) % count];
        // Districts past the exhausted budget get remaining == 0: they build no rows
        // this frame but still stamp from whatever rows they already have.
        remaining -= optimizer.GatherData(remaining, StampingEnabled);
      }
      _rotationStart = (_rotationStart + 1) % count;
    }

    #endregion

  }

}
