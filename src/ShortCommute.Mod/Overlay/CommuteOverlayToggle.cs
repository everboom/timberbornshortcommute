using Timberborn.CoreUI;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UILayoutSystem;
using UnityEngine.UIElements;

namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// Top-right toggle button that turns "commute analysis mode" on and off.
  /// Follows the vanilla <c>WaterOpacityTogglePanel</c> pattern (the same one
  /// Keystone's biome overlay uses): load the vanilla <c>Common/SquareToggle</c>
  /// prefab, register it via <see cref="UILayout.AddTopRightButton"/> on
  /// <see cref="ShowPrimaryUIEvent"/>, and flip a bool on click.
  ///
  /// <para><see cref="CommuteOverlayRenderer"/> polls <see cref="Enabled"/> each
  /// frame and starts/stops drawing accordingly. While off, the renderer draws
  /// nothing and the on-select path recompute can never fire.</para>
  ///
  /// <para>ShortCommute ships <b>no asset bundle</b>, so — unlike Keystone — the
  /// toggle uses no custom icon texture (loading one needs a bundle). It shows
  /// the vanilla checkmark and relies on a plain-text tooltip for discoverability.</para>
  /// </summary>
  public sealed class CommuteOverlayToggle : ILoadableSingleton {

    #region Constants

    private const string ToggleAsset = "Common/SquareToggle";
    private const string ShowTooltip = "Show commute analysis";
    private const string HideTooltip = "Hide commute analysis";
    private const int ButtonOrder = 10;

    #endregion

    #region Dependencies

    private readonly VisualElementLoader _visualElementLoader;
    private readonly UILayout _uiLayout;
    private readonly ITooltipRegistrar _tooltipRegistrar;
    private readonly EventBus _eventBus;

    #endregion

    #region State

    private VisualElement _root = null!;
    private Toggle _toggle = null!;
    private bool _enabled;

    /// <summary>Whether commute analysis mode is currently active.</summary>
    public bool Enabled => _enabled;

    #endregion

    public CommuteOverlayToggle(VisualElementLoader visualElementLoader, UILayout uiLayout,
        ITooltipRegistrar tooltipRegistrar, EventBus eventBus) {
      _visualElementLoader = visualElementLoader;
      _uiLayout = uiLayout;
      _tooltipRegistrar = tooltipRegistrar;
      _eventBus = eventBus;
    }

    #region Lifecycle

    /// <inheritdoc />
    public void Load() {
      _root = _visualElementLoader.LoadVisualElement(ToggleAsset);
      _toggle = _root.Q<Toggle>("Toggle");
      _tooltipRegistrar.Register(_root, () => _enabled ? HideTooltip : ShowTooltip);
      _toggle.RegisterValueChangedCallback(evt => _enabled = evt.newValue);
      _eventBus.Register(this);
    }

    /// <summary>Re-attach the button whenever the primary UI is (re)built.</summary>
    [OnEvent]
    public void OnShowPrimaryUI(ShowPrimaryUIEvent showPrimaryUIEvent) =>
        _uiLayout.AddTopRightButton(_root, ButtonOrder);

    #endregion

  }

}
