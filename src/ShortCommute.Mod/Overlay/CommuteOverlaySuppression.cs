namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// Shared flag telling the Harmony prefixes in <see cref="CommuteOverlayPatcher"/>
  /// whether the commute overlay is currently active. When <c>true</c>, the two
  /// vanilla selection-driven secondary highlighters (the building distance
  /// heatmap and the power-network highlight) are suppressed so they don't
  /// compete with — or overwrite — the overlay's own highlights and lines.
  ///
  /// <para>A plain static so the static prefix methods can read it without a
  /// service locator; written only by <see cref="CommuteOverlayRenderer"/> as it
  /// starts/stops drawing.</para>
  /// </summary>
  internal static class CommuteOverlaySuppression {

    /// <summary>True while the overlay is on and vanilla selection highlights
    /// should be suppressed.</summary>
    public static bool Active { get; set; }

  }

}
