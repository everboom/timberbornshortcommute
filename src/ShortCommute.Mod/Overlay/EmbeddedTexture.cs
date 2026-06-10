using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SylvanGames.ShortCommute.Overlay {

  /// <summary>
  /// Decodes a PNG embedded in this assembly into a runtime <see cref="Texture2D"/>
  /// for UI use — the mod's stand-in for an asset bundle. The overlay toggle uses
  /// this to put a custom icon on the vanilla button without shipping a Unity
  /// bundle: <c>element.style.backgroundImage = new StyleBackground(texture)</c>
  /// accepts a runtime-decoded <see cref="Texture2D"/> directly.
  /// </summary>
  /// <remarks>
  /// <para><b>Main thread only.</b> <see cref="Texture2D"/> construction and
  /// <see cref="ImageConversion.LoadImage(Texture2D, byte[])"/> are Unity
  /// main-thread APIs. Call from <c>ILoadableSingleton.Load()</c> or another
  /// main-thread context — never a worker thread.</para>
  /// <para><b>Loud on failure.</b> A missing or undecodable embedded resource is a
  /// build mistake, not a runtime condition to paper over, so both throw. The
  /// missing-resource message lists every embedded name in the assembly to make a
  /// wrong lookup string obvious.</para>
  /// </remarks>
  internal static class EmbeddedTexture {

    #region Decoding

    /// <summary>
    /// Loads and decodes the embedded PNG named <paramref name="resourceName"/>
    /// (its <c>LogicalName</c> in the csproj) into a fresh <see cref="Texture2D"/>.
    /// </summary>
    /// <param name="resourceName">The embedded resource's logical name.</param>
    /// <returns>A decoded, GPU-uploaded, no-longer-readable texture.</returns>
    /// <exception cref="InvalidOperationException">
    /// The resource is absent from the assembly or the bytes fail to decode.
    /// </exception>
    public static Texture2D LoadPng(string resourceName) {
      Assembly assembly = Assembly.GetExecutingAssembly();
      using Stream stream = assembly.GetManifestResourceStream(resourceName)
          ?? throw new InvalidOperationException(
              $"Embedded resource '{resourceName}' not found. Present: "
              + string.Join(", ", assembly.GetManifestResourceNames()));
      using var buffer = new MemoryStream();
      stream.CopyTo(buffer);

      // 2x2 is a throwaway size; LoadImage resizes to the PNG's real dimensions.
      var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false) {
        name = resourceName,
        wrapMode = TextureWrapMode.Clamp,
        filterMode = FilterMode.Bilinear,
      };
      if (!texture.LoadImage(buffer.ToArray())) {
        throw new InvalidOperationException(
            $"Failed to decode embedded PNG '{resourceName}'.");
      }
      texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
      return texture;
    }

    #endregion

  }

}
