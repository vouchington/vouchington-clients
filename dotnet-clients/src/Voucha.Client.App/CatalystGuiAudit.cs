#if DEBUG
using System.Text.Json.Nodes;

namespace Voucha.Client.App;

// Records app dispatch and fixture persistence, not delivery by an assistive technology.
internal sealed class CatalystGuiAudit
{
  private readonly object gate = new();
  private readonly string path;
  private readonly JsonObject report;
  private int activationId;

  internal static CatalystGuiAudit? Current { get; private set; }

  private CatalystGuiAudit(string path, JsonObject initialPreferences)
  {
    this.path = path;
    report = new JsonObject
    {
      ["schemaVersion"] = 1,
      ["initialPreferences"] = initialPreferences.DeepClone(),
      ["patches"] = new JsonArray(),
      ["activations"] = new JsonArray(),
      ["accessibilityDispatches"] = new JsonArray(),
    };
    Write();
  }

  internal static CatalystGuiAudit? Create(bool fixtureMode, string? resultPath, JsonObject preferences)
  {
    if (!fixtureMode || string.IsNullOrEmpty(resultPath)) return null;
    if (!Path.IsPathFullyQualified(resultPath)) throw new ArgumentException("An absolute GUI result path is required.", nameof(resultPath));
    return new CatalystGuiAudit(resultPath + ".app.json", preferences);
  }

  internal static CatalystGuiAudit? Start(JsonObject preferences)
  {
    Current = Create(true, Environment.GetEnvironmentVariable("CATALYST_GUI_RESULT"), preferences);
    return Current;
  }

  internal void Patch(JsonObject patch, JsonObject before, JsonObject after)
  {
    lock (gate)
    {
      var fields = new JsonArray();
      foreach (var field in patch) fields.Add(field.Key);
      var patches = report["patches"]!.AsArray();
      patches.Add(new JsonObject
      {
        ["fields"] = fields,
        ["before"] = before.DeepClone(),
        ["after"] = after.DeepClone(),
      });
      var written = false;
      try
      {
        Write();
        written = true;
      }
      finally
      {
        if (!written) patches.RemoveAt(patches.Count - 1);
      }
    }
  }

  internal void Activate(string canonicalPath)
  {
    lock (gate)
    {
      activationId++;
      report["activations"]!.AsArray().Add(new JsonObject
      {
        ["activationId"] = activationId,
        ["canonicalPath"] = canonicalPath,
      });
      Write();
    }
  }

  internal void Focus(string? automationId, bool succeeded) => Dispatch(new JsonObject
  {
    ["kind"] = "focus",
    ["automationId"] = automationId,
    ["succeeded"] = succeeded,
  });

  internal void Announce(string text) => Dispatch(new JsonObject
  {
    ["kind"] = "announcement",
    ["text"] = text,
  });

  private void Dispatch(JsonObject entry)
  {
    lock (gate)
    {
      entry["activationId"] = activationId;
      report["accessibilityDispatches"]!.AsArray().Add(entry);
      Write();
    }
  }

  private void Write()
  {
    var temporary = path + ".tmp";
    try
    {
      File.WriteAllText(temporary, report.ToJsonString());
      File.Move(temporary, path, overwrite: true);
    }
    finally
    {
      if (File.Exists(temporary)) File.Delete(temporary);
    }
  }
}
#endif
