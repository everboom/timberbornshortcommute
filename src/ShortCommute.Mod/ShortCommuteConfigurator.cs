using Bindito.Core;
using Timberborn.GameDistricts;
using Timberborn.TemplateInstantiation;
using UnityEngine;

namespace SylvanGames.ShortCommute {

  /// <summary>
  /// Wires ShortCommute into the game. A <see cref="CommuteOptimizer"/> is
  /// attached to every <see cref="DistrictCenter"/> as a template decorator —
  /// the same clean extension point the vanilla district uses, so no Harmony
  /// is required.
  /// </summary>
  [Context("Game")]
  public class ShortCommuteConfigurator : Configurator {

    /// <inheritdoc />
    protected override void Configure() {
      Bind<CommuteOptimizer>().AsTransient();
      MultiBind<TemplateModule>().ToProvider(ProvideTemplateModule).AsSingleton();
      Debug.Log("[ShortCommute] Configured — CommuteOptimizer decorates DistrictCenter.");
    }

    private static TemplateModule ProvideTemplateModule() {
      var builder = new TemplateModule.Builder();
      builder.AddDecorator<DistrictCenter, CommuteOptimizer>();
      return builder.Build();
    }

  }

}
