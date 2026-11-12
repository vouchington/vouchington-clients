#if DEBUG
using System.Text.Json.Nodes;
using Voucha.Client.Core.Diagnostics;

namespace Voucha.Client.App;

// Records app dispatch and fixture persistence, not delivery by an assistive technology.
internal sealed class CatalystGuiAudit
{
  private readonly CatalystGuiAuditStore store;

  internal static CatalystGuiAudit? Current { get; private set; }

  private CatalystGuiAudit(CatalystGuiAuditStore store) => this.store = store;

  internal static CatalystGuiAudit? Create(bool fixtureMode, string? resultPath, JsonObject preferences)
  {
    var store = CatalystGuiAuditStore.Create(fixtureMode, resultPath, preferences);
    return store is null ? null : new CatalystGuiAudit(store);
  }

  internal static CatalystGuiAudit? Start(JsonObject preferences)
  {
    Current = Create(true, Environment.GetEnvironmentVariable("CATALYST_GUI_RESULT"), preferences);
    return Current;
  }

  internal void Patch(JsonObject patch, JsonObject before, JsonObject after) => store.Patch(patch, before, after);

  internal void Activate(string canonicalPath) => store.Activate(canonicalPath);

  internal void Focus(string? automationId, bool succeeded) => store.Dispatch(new JsonObject
  {
    ["kind"] = "focus",
    ["automationId"] = automationId,
    ["succeeded"] = succeeded,
  });

  internal void Announce(string text) => store.Dispatch(new JsonObject
  {
    ["kind"] = "announcement",
    ["text"] = text,
  });
}
#endif
