#if DEBUG
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Voucha.Client.App;

internal sealed partial class CatalystGuiFixtureHandler
{
  internal static HttpMessageHandler Wrap(HttpMessageHandler inner)
  {
    if (!CatalystGuiPreflight.TryReadFixtures(out var identity, out var preferences)) return inner;
    try
    {
      if (JsonNode.Parse(preferences) is not JsonObject document || document["email_preferences"] is not JsonObject)
        return inner;
      return new CatalystGuiFixtureHandler(inner, identity, document, CatalystGuiAudit.Start(document["email_preferences"]!.AsObject()));
    }
    catch (JsonException)
    {
      return inner;
    }
  }
}
#endif
