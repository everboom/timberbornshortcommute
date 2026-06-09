using System.Reflection;
using HarmonyLib;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// Applies the overlay's two surgical Harmony patches once at game load. Both
  /// are prefixes gated on <see cref="CommuteOverlaySuppression.Active"/> that
  /// skip a single vanilla method while the commute overlay is on:
  /// <list type="bullet">
  ///   <item><c>DistanceHeatmapShower.ShowHeatmap</c> — the vanilla building
  ///   distance heatmap shown on selecting any building with a path range. It
  ///   paints the same <c>Secondary</c> highlight layer the overlay uses, so it
  ///   would overwrite the overlay's commute colours.</item>
  ///   <item><c>MechanicalGraphHighlightService.HighlightSelectedNode</c> — the
  ///   power-network highlight shown on selecting a powered building.</item>
  /// </list>
  ///
  /// <para>These are the <em>only</em> Harmony patches in the mod — narrow,
  /// reversible (gated by a flag, not a behaviour rewrite), and inert while the
  /// overlay is off (the prefix returns <c>true</c>, so vanilla runs untouched).
  /// Both targets are <c>internal</c> in other assemblies, so they're resolved by
  /// name via <see cref="AccessTools"/> and patched manually rather than by
  /// attribute.</para>
  /// </summary>
  public sealed class CommuteOverlayPatcher : ILoadableSingleton {

    #region Constants

    private const string HarmonyId = "SylvanGames.ShortCommute.Overlay";

    #endregion

    #region State

    /// <summary>Guard so a save reload (which re-runs Game-context Load) doesn't
    /// re-apply the patches.</summary>
    private static bool _patched;

    #endregion

    #region Lifecycle

    /// <inheritdoc />
    public void Load() {
      if (_patched) {
        return;
      }
      try {
        var harmony = new Harmony(HarmonyId);
        var applied = 0;
        applied += PatchSuppression(harmony,
            "Timberborn.DistanceHeatmap.DistanceHeatmapShower", "ShowHeatmap") ? 1 : 0;
        applied += PatchSuppression(harmony,
            "Timberborn.MechanicalSystemHighlighting.MechanicalGraphHighlightService",
            "HighlightSelectedNode") ? 1 : 0;
        _patched = true;
        Debug.Log($"[ShortCommute] Overlay suppression: applied {applied}/2 Harmony prefixes.");
      } catch (System.Exception ex) {
        // Loud, but non-fatal: the overlay still works, just without suppression.
        Debug.LogError($"[ShortCommute] Overlay suppression patches failed to apply "
                       + $"(is 0Harmony.dll loaded?): {ex}");
      }
    }

    #endregion

    #region Patching

    /// <summary>Prefix the named (instance, parameterless) method with the
    /// suppression gate. Fails loud — but non-fatally — if a game update renamed
    /// or moved the target: the overlay still works, just without suppression.</summary>
    private static bool PatchSuppression(Harmony harmony, string typeName, string methodName) {
      var type = AccessTools.TypeByName(typeName);
      if (type == null) {
        Debug.LogError($"[ShortCommute] Overlay suppression: type '{typeName}' not found — "
                       + "vanilla highlight will not be suppressed under the overlay.");
        return false;
      }
      var method = AccessTools.Method(type, methodName);
      if (method == null) {
        Debug.LogError($"[ShortCommute] Overlay suppression: method '{typeName}.{methodName}' not "
                       + "found — vanilla highlight will not be suppressed under the overlay.");
        return false;
      }
      harmony.Patch(method, prefix: new HarmonyMethod(SkipWhenOverlayActiveMethod));
      return true;
    }

    private static readonly MethodInfo SkipWhenOverlayActiveMethod =
        AccessTools.Method(typeof(CommuteOverlayPatcher), nameof(SkipWhenOverlayActive));

    /// <summary>Harmony prefix: returning <c>false</c> skips the original. We skip
    /// (suppress the vanilla highlight) exactly when the overlay is active.</summary>
    private static bool SkipWhenOverlayActive() => !CommuteOverlaySuppression.Active;

    #endregion

  }

}
