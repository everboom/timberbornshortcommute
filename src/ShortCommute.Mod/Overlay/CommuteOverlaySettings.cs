using ModSettings.Core;
using Timberborn.Modding;
using Timberborn.SettingsSystem;

namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// In-game mod settings for the commute overlay (rendered in the game's
  /// Mods settings panel via the eMka.ModSettings dependency).
  ///
  /// <para>Currently one opt-in toggle: while the commute overlay is on, suppress
  /// the vanilla path/nav range mesh that path-range buildings (dwellings,
  /// ziplines, tubeways) draw on selection. That vanilla overlay
  /// (<c>DistrictPathNavRangeDrawer</c>) re-assembles its whole per-tile mesh as
  /// the selected/hovered target changes and can tank the frame rate on large
  /// path networks. <b>On by default</b> (the overlay supersedes it and the
  /// frame-rate win is the point); the suppression only ever applies while the
  /// overlay is active, so vanilla is untouched when the overlay is off (see
  /// <see cref="CommuteOverlayPatcher"/>). Turn it off to keep the vanilla mesh.</para>
  ///
  /// <para>The toggle's live value is mirrored into
  /// <see cref="CommuteOverlaySuppression.HidePathRange"/> by
  /// <see cref="CommuteOverlayRenderer"/>, which the Harmony prefix reads.</para>
  /// </summary>
  public class CommuteOverlaySettings : ModSettingsOwner {

    /// <summary>When on, the commute overlay hides the vanilla path-range mesh
    /// while it is active. On by default.</summary>
    public ModSetting<bool> HidePathRangeOverlay { get; } =
        new(true,
            ModSettingDescriptor
                .Create("Hide path-range overlay during commute analysis")
                .SetTooltip("While the commute analysis overlay is on, suppress the vanilla "
                            + "road/nav range mesh that path-range buildings (dwellings, "
                            + "ziplines, tubeways) draw when selected.\n\n"
                            + "That overlay rebuilds its whole mesh as the selected or hovered "
                            + "building changes, and can drop the frame rate badly on large "
                            + "path networks.\n\n"
                            + "On by default; turn it off to keep the vanilla mesh. Vanilla "
                            + "behaviour is unchanged whenever the commute overlay is off."));

    /// <inheritdoc />
    public override ModSettingsContext ChangeableOn => ModSettingsContext.All;

    /// <inheritdoc />
    protected override string ModId => "SylvanGames.ShortCommute";

    public CommuteOverlaySettings(ISettings settings,
        ModSettingsOwnerRegistry modSettingsOwnerRegistry, ModRepository modRepository)
        : base(settings, modSettingsOwnerRegistry, modRepository) {}

  }

}
