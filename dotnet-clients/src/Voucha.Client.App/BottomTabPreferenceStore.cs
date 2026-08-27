using System.Text.Json;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed class BottomTabPreferenceStore
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
  private readonly object gate = new();
  private readonly string path;

  public BottomTabPreferenceStore()
      : this(Path.Combine(FileSystem.AppDataDirectory, "bottom-tab-preferences.json"))
  {
  }

  public BottomTabPreferenceStore(string path) =>
      this.path = path ?? throw new ArgumentNullException(nameof(path));

  public event EventHandler? PreferencesChanged;

  public BottomTabPreferences Load()
  {
    lock (gate)
    {
      if (!File.Exists(path))
      {
        return BottomTabPreferences.Default;
      }

      try
      {
        var data = File.ReadAllText(path);
        var preferences = JsonSerializer.Deserialize<BottomTabPreferences>(data, JsonOptions);
        return preferences?.OrderedIntentIds is null || preferences.HiddenIntentIds is null
            ? BottomTabPreferences.Default
            : preferences;
      }
      catch (IOException)
      {
        return BottomTabPreferences.Default;
      }
      catch (JsonException)
      {
        return BottomTabPreferences.Default;
      }
      catch (UnauthorizedAccessException)
      {
        return BottomTabPreferences.Default;
      }
    }
  }

  public void Save(BottomTabPreferences preferences)
  {
    ArgumentNullException.ThrowIfNull(preferences);

    lock (gate)
    {
      try
      {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
          Directory.CreateDirectory(directory);
        }

        var data = JsonSerializer.Serialize(preferences, JsonOptions);
        File.WriteAllText(path, data);
      }
      catch (IOException)
      {
        return;
      }
      catch (UnauthorizedAccessException)
      {
        return;
      }
    }

    PreferencesChanged?.Invoke(this, EventArgs.Empty);
  }
}
