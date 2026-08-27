using Voucha.Client.App.Pages;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private bool TryCreateInitialEngineeringAgentRoot(NativeRouteMatch match, out Page page)
  {
    if (match.Param("idOrSlug") is not { } agent)
    {
      page = null!;
      return false;
    }

    var agentListsPage = serviceProvider.GetRequiredService<AgentListsPage>();
    agentListsPage.SetNavigator(OpenNativePathAsync);
    agentListsPage.SetContext();
    agentListsPage.SetInitialTarget(
        match.Param("conversationId") is { } conversation
            ? AgentNavigationTargets.Conversation(agent, conversation)
            : AgentNavigationTargets.Detail(agent, AgentConversationFilter.FromQuery(match.QueryItems)));
    page = agentListsPage;
    return true;
  }

  private async Task<bool> TryPrepareEngineeringAgentRouteAsync(
      Page shellContentPage,
      NativeRouteMatch match,
      Page targetPage)
  {
    if (match.Param("idOrSlug") is not { } agent)
    {
      return false;
    }

    var navigation = shellContentPage.Navigation;
    var directoryPage = navigation.NavigationStack.OfType<AgentListsPage>().LastOrDefault();
    if (directoryPage is null)
    {
      await navigation.PopToRootAsync(animated: false).ConfigureAwait(true);
      directoryPage = serviceProvider.GetRequiredService<AgentListsPage>();
      directoryPage.SetNavigator(OpenNativePathAsync);
      directoryPage.SetContext();
      await navigation.PushAsync(directoryPage, animated: false).ConfigureAwait(true);
    }

    if (targetPage is AgentDetailPage)
    {
      await PopToPageAsync(navigation, directoryPage).ConfigureAwait(true);
      await navigation.PushAsync(targetPage).ConfigureAwait(true);
      return true;
    }

    if (targetPage is not AgentConversationPage)
    {
      return false;
    }

    if (navigation.NavigationStack.LastOrDefault() is not AgentDetailPage currentDetail ||
        !currentDetail.MatchesContext(agent))
    {
      await PopToPageAsync(navigation, directoryPage).ConfigureAwait(true);
      var parentDetailPage = serviceProvider.GetRequiredService<AgentDetailPage>();
      parentDetailPage.SetNavigator(OpenNativePathAsync);
      parentDetailPage.SetContext(agent);
      await navigation.PushAsync(parentDetailPage, animated: false).ConfigureAwait(true);
    }

    await navigation.PushAsync(targetPage).ConfigureAwait(true);
    return true;
  }

  private static async Task PopToPageAsync(INavigation navigation, Page page)
  {
    if (!navigation.NavigationStack.Contains(page)) return;
    for (var remainingPages = navigation.NavigationStack.Count;
         remainingPages > 0 && navigation.NavigationStack.LastOrDefault() is { } current && !ReferenceEquals(current, page);
         remainingPages--)
    {
      await navigation.PopAsync(animated: false).ConfigureAwait(true);
    }
  }
}
