#if DEBUG
using System.ComponentModel;

namespace Voucha.Client.App.Pages;

public sealed partial class NotificationPreferencesView
{
  partial void InitializeControlAudit()
  {
    PropertyChanged += OnAuditPropertyChanged;
    EngagementSwitch.PropertyChanged += OnAuditPropertyChanged;
    ModerationSwitch.PropertyChanged += OnAuditPropertyChanged;
    ScheduleControls.PropertyChanged += OnAuditPropertyChanged;
    ParentChanged += OnAuditParentChanged;
  }

  private readonly List<VisualElement> auditedAncestors = [];

  private void OnAuditParentChanged(object? sender, EventArgs args)
  {
    foreach (var ancestor in auditedAncestors)
    {
      ancestor.PropertyChanged -= OnAuditPropertyChanged;
      ancestor.ParentChanged -= OnAuditParentChanged;
    }
    auditedAncestors.Clear();
    for (Element? parent = Parent; parent is not null; parent = parent.Parent)
    {
      if (parent is not VisualElement visual) continue;
      auditedAncestors.Add(visual);
      visual.PropertyChanged += OnAuditPropertyChanged;
      visual.ParentChanged += OnAuditParentChanged;
    }
    RecordControlAudit();
  }

  private void OnAuditPropertyChanged(object? sender, PropertyChangedEventArgs args)
  {
    if (args.PropertyName is nameof(IsVisible) or nameof(IsEnabled) or nameof(Switch.IsToggled))
      RecordControlAudit();
  }

  partial void RecordControlAudit()
  {
    var visible = EffectivelyVisible(this);
    CatalystGuiAudit.Current?.RecordControls(
        EngagementSwitch.IsToggled,
        ModerationSwitch.IsToggled,
        EffectivelyVisible(ScheduleControls),
        visible && model is not null && acceptControlEvents && !isSynchronizing &&
        EngagementSwitch.IsEnabled && ModerationSwitch.IsEnabled);
  }

  private static bool EffectivelyVisible(VisualElement control)
  {
    for (Element? element = control; element is not null; element = element.Parent)
      if (element is VisualElement visual && !visual.IsVisible) return false;
    return true;
  }
}
#endif
