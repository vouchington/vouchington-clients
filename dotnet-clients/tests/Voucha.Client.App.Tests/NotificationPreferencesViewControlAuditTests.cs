#if DEBUG
using System.Text.Json.Nodes;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class NotificationPreferencesViewTests
{
  [Fact]
  public async Task AppliedControlAuditWaitsForEventReleaseAndTracksActualVisibilityAndEnabledState()
  {
    var dispatcher = new AuditDispatcher();
    DispatcherProvider.SetCurrent(dispatcher);
    _ = new Application { Resources = { ["Headline"] = new Style(typeof(Label)), ["Body"] = new Style(typeof(Label)) } };
    var directory = Path.Combine(Path.GetTempPath(), "voucha-controls-" + Guid.NewGuid());
    Directory.CreateDirectory(directory);
    var result = Path.Combine(directory, "result.json");
    var previous = Environment.GetEnvironmentVariable("CATALYST_GUI_RESULT");
    JsonNode Controls() => JsonNode.Parse(File.ReadAllText(result + ".app.json"))!["controls"]!;
    try
    {
      Environment.SetEnvironmentVariable("CATALYST_GUI_RESULT", result);
      CatalystGuiAudit.Start(new JsonObject());
      var service = new SettingsService();
      using var model = new NotificationPreferencesViewModel(service);
      var view = new NotificationPreferencesView { BindingContext = model };
      var parent = new VerticalStackLayout { Children = { view } };
      await model.LoadAsync(TestContext.Current.CancellationToken);
      view.SynchronizeControls();
      Assert.False(Controls()["ready"]!.GetValue<bool>());
      dispatcher.Release();
      Assert.True(Controls()["ready"]!.GetValue<bool>());
      Assert.Equal(model.EngagementEmailsEnabled, Controls()["engagement"]!.GetValue<bool>());
      Assert.True(Controls()["scheduleVisible"]!.GetValue<bool>());

      service.Preferences = service.Preferences with { ModerationEmailsEnabled = false };
      await model.SetModerationEmailsAsync(false, TestContext.Current.CancellationToken);
      dispatcher.Release();
      Assert.False(Controls()["moderation"]!.GetValue<bool>());
      Assert.False(Controls()["scheduleVisible"]!.GetValue<bool>());

      // The audit must follow actual controls even when their state differs from the model.
      view.FindByName<VerticalStackLayout>("ScheduleControls").IsVisible = true;
      Assert.True(Controls()["scheduleVisible"]!.GetValue<bool>());
      var engagement = Find<Switch>(view, "notification-engagement");
      engagement.IsEnabled = false;
      Assert.False(Controls()["ready"]!.GetValue<bool>());
      engagement.IsEnabled = true;
      Assert.True(Controls()["ready"]!.GetValue<bool>());
      parent.IsVisible = false;
      Assert.False(Controls()["ready"]!.GetValue<bool>());
      Assert.False(Controls()["scheduleVisible"]!.GetValue<bool>());
      parent.IsVisible = true;
      Assert.True(Controls()["ready"]!.GetValue<bool>());
    }
    finally
    {
      Environment.SetEnvironmentVariable("CATALYST_GUI_RESULT", null);
      CatalystGuiAudit.Start(new JsonObject());
      Environment.SetEnvironmentVariable("CATALYST_GUI_RESULT", previous);
      Directory.Delete(directory, true);
      DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    }
  }

  private sealed class AuditDispatcher : IDispatcher, IDispatcherProvider
  {
    private readonly Queue<Action> pending = new();
    public bool IsDispatchRequired => false;
    public IDispatcher GetForCurrentThread() => this;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { pending.Enqueue(action); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    public void Release() { while (pending.TryDequeue(out var action)) action(); }
  }
}
#endif
