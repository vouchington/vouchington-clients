namespace Voucha.Client.Core.Tags;

public sealed partial class TagManagementViewModel
{
  public void OnUiLocaleChanged()
  {
    if (Context is null) return;
    Tabs = TabsFor(Context);
    SelectedTab = SelectRequestedTab(SelectedTab?.Value ?? Context.ObjectType);
    RefreshTitle();
  }

  public void Dispose() => localeSubscription?.Dispose();

  private TagRelationTab? SelectRequestedTab(string requested) =>
      Tabs.FirstOrDefault(tab => tab.Value == requested);
}
