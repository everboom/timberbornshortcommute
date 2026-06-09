using System.Collections.Generic;
using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.DwellingSystem;
using Timberborn.EntitySystem;
using Timberborn.Navigation;
using Timberborn.SelectionSystem;
using Timberborn.SingletonSystem;
using Timberborn.WorkSystem;
using UnityEngine;

namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// Drives the commute overlay while <see cref="CommuteOverlayToggle"/> is on.
  /// Context-sensitive by selection:
  /// <list type="bullet">
  ///   <item><b>Nothing selected</b> — a whole-map heatmap: every dwelling is
  ///   secondary-highlighted with the band of its <em>worst</em> occupant's
  ///   commute (<see cref="CommuteBands"/>).</item>
  ///   <item><b>House selected</b> — straight coloured lines to each workplace
  ///   its occupants work at.</item>
  ///   <item><b>Workplace selected</b> — straight coloured lines to the homes its
  ///   workers live in.</item>
  ///   <item><b>Beaver selected</b> — the walked path from its home to its
  ///   workplace (one pathfind, off the optimizer's tick path).</item>
  /// </list>
  ///
  /// <para>The heatmap uses an isolated <see cref="Highlighter"/> instance (bound
  /// transient, so ours is our own) writing the <c>Secondary</c> layer — the same
  /// approach the vanilla power-network overlay uses — so it rides alongside the
  /// game's selection highlight (which owns the <c>Primary</c> layer) rather than
  /// fighting it. Heatmap colours are only re-applied when a house's band changes;
  /// the per-frame cost is otherwise a band recompute and no Highlighter churn.</para>
  /// </summary>
  public sealed class CommuteOverlayRenderer : ILoadableSingleton, IUpdatableSingleton {

    #region Constants

    /// <summary>Vertical lift applied to line endpoints/corners so lines float
    /// just above the ground instead of z-fighting terrain.</summary>
    private const float LineHeight = 0.5f;

    #endregion

    #region Dependencies

    private readonly CommuteOverlayToggle _toggle;
    private readonly EntityComponentRegistry _entityRegistry;
    private readonly EntitySelectionService _selectionService;
    private readonly CommuteLineDrawer _lines;
    private readonly Highlighter _highlighter;
    private readonly EventBus _eventBus;

    #endregion

    #region State

    /// <summary>True while the overlay is actively drawing (toggle on).</summary>
    private bool _active;

    /// <summary>Dwellings we have secondary-highlighted, and the band colour each
    /// currently shows — so we only re-highlight on a band change.</summary>
    private readonly Dictionary<Dwelling, Color> _highlighted = new();

    /// <summary>Reusable buffers for the beaver path draw (avoid per-select alloc).</summary>
    private readonly List<PathCorner> _cornerBuffer = new();
    private readonly List<Vector3> _pathBuffer = new();

    #endregion

    public CommuteOverlayRenderer(CommuteOverlayToggle toggle, EntityComponentRegistry entityRegistry,
        EntitySelectionService selectionService, CommuteLineDrawer lines, Highlighter highlighter,
        EventBus eventBus) {
      _toggle = toggle;
      _entityRegistry = entityRegistry;
      _selectionService = selectionService;
      _lines = lines;
      _highlighter = highlighter;
      _eventBus = eventBus;
    }

    #region Lifecycle

    /// <inheritdoc />
    public void Load() => _eventBus.Register(this);

    /// <inheritdoc />
    public void UpdateSingleton() {
      if (_toggle.Enabled) {
        if (!_active) {
          _active = true;
          RedrawSelection(); // pick up whatever is already selected when mode turns on
        }
        RefreshHeatmap();
      } else if (_active) {
        _active = false;
        ClearAll();
      }
    }

    #endregion

    #region Selection handling

    [OnEvent]
    public void OnObjectSelected(SelectableObjectSelectedEvent selectedEvent) {
      if (_active) {
        DrawForSelection(selectedEvent.SelectableObject);
      }
    }

    [OnEvent]
    public void OnObjectUnselected(SelectableObjectUnselectedEvent unselectedEvent) =>
        _lines.Clear();

    private void RedrawSelection() {
      var selected = _selectionService.SelectedObject;
      if (selected != null) {
        DrawForSelection(selected);
      }
    }

    /// <summary>Dispatch by what was selected — beaver, house, or workplace.</summary>
    private void DrawForSelection(SelectableObject selectable) {
      _lines.Clear();
      if (selectable == null) {
        return;
      }
      // A beaver carries a Worker; buildings carry Dwelling / Workplace. These are
      // mutually exclusive entities, so first match wins.
      if (selectable.GetComponent<Worker>() is { } worker) {
        DrawBeaverPath(worker);
      } else if (selectable.GetComponent<Dwelling>() is { } dwelling) {
        DrawHouseLines(dwelling);
      } else if (selectable.GetComponent<Workplace>() is { } workplace) {
        DrawWorkplaceLines(workplace);
      }
    }

    #endregion

    #region Heatmap

    private void RefreshHeatmap() {
      foreach (var dwelling in _entityRegistry.GetEnabled<Dwelling>()) {
        var band = WorstBand(dwelling);
        if (band is { } color) {
          if (!_highlighted.TryGetValue(dwelling, out var current) || current != color) {
            _highlighter.HighlightSecondary(dwelling, color);
            _highlighted[dwelling] = color;
          }
        } else if (_highlighted.ContainsKey(dwelling)) {
          _highlighter.UnhighlightSecondary(dwelling);
          _highlighted.Remove(dwelling);
        }
      }
    }

    /// <summary>The band of the worst (longest) measured commute among a
    /// dwelling's adult occupants, or <c>null</c> when none has data — the worst
    /// commuter drives the house's colour. Children don't work, so they're
    /// ignored.</summary>
    private static Color? WorstBand(Dwelling dwelling) {
      var worst = float.NaN;
      foreach (var dweller in dwelling.AdultDwellers) {
        if (dweller.GetComponent<Worker>() is not { Employed: true } worker) {
          continue;
        }
        var cost = worker.GetComponent<CommuteCost>();
        if (cost == null || !cost.HasData) {
          continue;
        }
        if (float.IsNaN(worst) || cost.RoadDistance > worst) {
          worst = cost.RoadDistance;
        }
      }
      return CommuteBands.Resolve(worst);
    }

    #endregion

    #region Line / path draws

    private void DrawHouseLines(Dwelling dwelling) {
      var from = Center(dwelling);
      if (from is not { } origin) {
        return;
      }
      foreach (var dweller in dwelling.AdultDwellers) {
        if (dweller.GetComponent<Worker>() is not { Employed: true } worker) {
          continue;
        }
        var workplace = worker.Workplace;
        if (workplace == null || Center(workplace) is not { } target) {
          continue;
        }
        _lines.DrawSegment(origin, target, LineColor(worker));
      }
    }

    private void DrawWorkplaceLines(Workplace workplace) {
      var from = Center(workplace);
      if (from is not { } origin) {
        return;
      }
      foreach (var worker in workplace.AssignedWorkers) {
        var dweller = worker.GetComponent<Dweller>();
        if (dweller == null || !dweller.HasHome || Center(dweller.Home) is not { } target) {
          continue;
        }
        _lines.DrawSegment(origin, target, LineColor(worker));
      }
    }

    private void DrawBeaverPath(Worker worker) {
      var workplace = worker.Workplace;
      if (workplace == null) {
        return; // unemployed beaver: no commute to draw
      }
      var dweller = worker.GetComponent<Dweller>();
      if (dweller == null || !dweller.HasHome || dweller.HomeAccess is not { } start) {
        return;
      }
      var workplaceAccessible = workplace.GetEnabledComponent<Accessible>();
      if (workplaceAccessible == null) {
        return;
      }
      _cornerBuffer.Clear();
      if (!workplaceAccessible.FindPathUnlimitedRange(start, _cornerBuffer, out _)) {
        return;
      }
      _pathBuffer.Clear();
      foreach (var corner in _cornerBuffer) {
        _pathBuffer.Add(corner.Position + Vector3.up * LineHeight);
      }
      _lines.DrawPolyline(_pathBuffer, LineColor(worker));
    }

    #endregion

    #region Helpers

    private void ClearAll() {
      foreach (var dwelling in _highlighted.Keys) {
        _highlighter.UnhighlightSecondary(dwelling);
      }
      _highlighted.Clear();
      _lines.Clear();
    }

    /// <summary>World-space line endpoint for a building: its grounded centre,
    /// lifted slightly. Null if the component carries no <see cref="BlockObjectCenter"/>.</summary>
    private static Vector3? Center(BaseComponent component) {
      var center = component.GetComponent<BlockObjectCenter>();
      return center == null ? null : center.WorldCenterGrounded + Vector3.up * LineHeight;
    }

    private static Color LineColor(Worker worker) {
      var cost = worker.GetComponent<CommuteCost>();
      return CommuteBands.ResolveOrNeutral(cost != null ? cost.RoadDistance : float.NaN);
    }

    #endregion

  }

}
