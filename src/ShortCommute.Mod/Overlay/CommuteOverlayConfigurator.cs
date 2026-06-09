using Bindito.Core;

namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// Binds the commute overlay singletons. All three implement Timberborn
  /// lifecycle interfaces (<c>ILoadableSingleton</c> / <c>IUpdatableSingleton</c>)
  /// which Bindito honours for bound singletons, so binding here is all the wiring
  /// the overlay needs — no Harmony, no template decorators (the overlay reads the
  /// <see cref="CommuteCost"/> the main mod already stamps).
  /// </summary>
  [Context("Game")]
  public class CommuteOverlayConfigurator : Configurator {

    /// <inheritdoc />
    protected override void Configure() {
      Bind<CommuteOverlayToggle>().AsSingleton();
      Bind<CommuteLineDrawer>().AsSingleton();
      Bind<CommuteOverlayRenderer>().AsSingleton();
    }

  }

}
