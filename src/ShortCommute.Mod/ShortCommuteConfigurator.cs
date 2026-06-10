using Bindito.Core;
using Timberborn.GameDistricts;
using Timberborn.TemplateInstantiation;
using Timberborn.WorkSystem;
using UnityEngine;

namespace SylvanGames.ShortCommute {

  /// <summary>
  /// Wires ShortCommute into the game. A <see cref="CommuteOptimizer"/> is
  /// attached to every <see cref="DistrictCenter"/> as a template decorator —
  /// the same clean extension point the vanilla district uses, so the core needs
  /// no Harmony (the overlay uses two narrow, flag-gated Harmony prefixes — see
  /// <c>Overlay/CommuteOverlayPatcher.cs</c>). A <see cref="CommuteCost"/> is attached to every
  /// <see cref="Worker"/> so the optimizer can stamp each beaver's measured
  /// work-path distance for the commute overlay to read. The singleton
  /// <see cref="CommuteDataService"/> drives every optimizer's read-only data
  /// gathering on the frame loop (so it runs even while paused).
  /// </summary>
  [Context("Game")]
  public class ShortCommuteConfigurator : Configurator {

    /// <inheritdoc />
    protected override void Configure() {
      Bind<CommuteDataService>().AsSingleton();
      Bind<CommuteOptimizer>().AsTransient();
      Bind<CommuteCost>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
      Debug.Log("[ShortCommute] Configured — CommuteOptimizer decorates DistrictCenter; "
                + "CommuteCost decorates Worker.");
    }

    private static TemplateModule ProvideTemplateModule() {
      var builder = new TemplateModule.Builder();
      builder.AddDecorator<DistrictCenter, CommuteOptimizer>();
      builder.AddDecorator<Worker, CommuteCost>();
      return builder.Build();
    }

  }

}
