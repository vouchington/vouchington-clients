using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

internal sealed class ModerationActionErrorLabel : Label
{
  public ModerationActionErrorLabel(ObservableObject model, string errorProperty, string hasErrorProperty, string automationId)
  {
    AutomationId = automationId;
    TextColor = Colors.IndianRed;
    SetBinding(TextProperty, new Binding(errorProperty, source: model));
    SetBinding(IsVisibleProperty, new Binding(hasErrorProperty, source: model));
  }

  public void ClearPrivateState()
  {
    RemoveBinding(TextProperty);
    RemoveBinding(IsVisibleProperty);
    IsVisible = false;
    BindingContext = null;
    Text = null;
  }
}
