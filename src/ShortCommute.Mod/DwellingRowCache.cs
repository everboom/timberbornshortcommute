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
  /// Caches <see cref="DwellingRow"/>s for the current rebalance pass, with
  /// <b>block clustering</b>. A pure store: it builds and holds rows but owns no
  /// budget — the per-frame fill ceiling lives in <see cref="CommuteDataService"/>,
  /// which calls <see cref="BuildRow"/> only when it can afford a fill. The shuffle
  /// (on the tick) only ever reads, via <see cref="TryGetCachedRow"/>, and never
  /// triggers a fill.
  ///
  /// <para><b>Dwelling-rooted.</b> A road-path query is a Dijkstra flow-field
  /// fill rooted at the start node, so rooting at the dwelling yields its
  /// distance to every workplace in one fill. Dwelling count is floored by
  /// dwelling capacity (it can't balloon like the workplace count), and swap
  /// evaluation becomes fill-free (its distances are lookups in two already-built
  /// rows).</para>
  ///
  /// <para><b>Clustering.</b> When we fill a dwelling's field, that field also
  /// holds its distance to every <em>other</em> dwelling — free lookups. Any
  /// dwelling within <c>clusterRadius</c> road-tiles is treated as part of the
  /// same block and handed the rep's row instead of getting its own fill. By the
  /// triangle inequality the error is bounded by the radius; with a ~10-tile
  /// "same block" radius that's well below typical commutes on a large map, so
  /// the cost is a small, bounded suboptimality on near-threshold moves while one
  /// fill covers an entire housing block. (Houses spread far apart — the layout
  /// this mod exists to support — simply don't cluster and keep their own fills.)</para>
  ///
  /// <para><b>Lifetime.</b> Cached rows live for the whole pass
  /// (<see cref="Clear"/> at day start). Because a row is dwelling-&gt;workplace
  /// road distance — a function of geometry, not of who lives where — moving a
  /// beaver never invalidates a row, so the gatherer can build rows on the frame
  /// loop while the shuffle consumes them on the tick with no coherence hazard.</para>
  /// </summary>
  internal sealed class DwellingRowCache {

    private readonly Dictionary<Dwelling, DwellingRow> _rows = new();
    private readonly float _clusterRadius;

    /// <summary>Active dwellings this pass — the pool clustering draws members from.</summary>
    private IReadOnlyList<Dwelling> _clusterDwellings = System.Array.Empty<Dwelling>();

    public DwellingRowCache(float clusterRadius) => _clusterRadius = clusterRadius;

    /// <summary>Drop all cached rows (call at the start of each rebalance pass).</summary>
    public void Clear() => _rows.Clear();

    /// <summary>Provide the active-dwelling list used to find cluster members (call before building).</summary>
    public void SetClusterDwellings(IReadOnlyList<Dwelling> dwellings) => _clusterDwellings = dwellings;

    /// <summary>True when <paramref name="dwelling"/> already has a built row
    /// (including one inherited as a cluster member).</summary>
    public bool HasRow(Dwelling dwelling) => _rows.ContainsKey(dwelling);

    /// <summary>The dwelling's row if built, otherwise <c>null</c>. Never fills —
    /// this is the read-only accessor the shuffle uses; a <c>null</c> means "not
    /// gathered yet", and the caller defers.</summary>
    public DwellingRow? TryGetCachedRow(Dwelling dwelling) =>
        _rows.TryGetValue(dwelling, out var row) ? row : null;

    /// <summary>
    /// Build (and cluster) the dwelling's row — one fill rooted at it — and cache
    /// it. Idempotent: a dwelling that already has a row (e.g. inherited as a
    /// cluster member) returns it without a second fill, so a caller that checked
    /// <see cref="HasRow"/> first spends exactly one fill per call.
    /// </summary>
    public DwellingRow BuildRow(Dwelling dwelling, IReadOnlyList<Workplace> workplaces) =>
        _rows.TryGetValue(dwelling, out var existing)
            ? existing
            : BuildAndCluster(dwelling, workplaces);

    private DwellingRow BuildAndCluster(Dwelling rep, IReadOnlyList<Workplace> workplaces) {
      var row = Build(rep, workplaces); // the one fill, rooted at rep
      _rows[rep] = row;

      // The rep's flow field is now warm, so rep -> each other dwelling is a cheap
      // lookup. Dwellings within the block radius share the rep's row (their true
      // distances differ by at most that radius), so this one fill covers the block.
      if (_clusterRadius > 0f) {
        var repAccessible = rep.GetEnabledComponent<Accessible>();
        if (repAccessible != null) {
          for (var i = 0; i < _clusterDwellings.Count; i++) {
            var other = _clusterDwellings[i];
            if (other == rep || _rows.ContainsKey(other)) {
              continue;
            }
            var otherAccessible = other.GetEnabledComponent<Accessible>();
            if (otherAccessible != null
                && repAccessible.FindRoadPath(otherAccessible, out var distance)
                && distance <= _clusterRadius) {
              _rows[other] = row; // clustered: reuse the rep's row (approximate within the radius)
            }
          }
        }
      }
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
            distances[workplace] = 0f;
          }
        }
      }
      return new DwellingRow(distances);
    }

  }

}
