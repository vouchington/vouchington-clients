using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Settings load failures surface in view state.")]
  private async Task LoadSettingsContentAsync(User user, string userIdOrSlug, int generation, CancellationToken cancellationToken)
  {
    try
    {
      var profileTask = settingsService.FetchMyProfileAsync(cancellationToken);
      var profileLinksTask = settingsService.FetchProfileLinksAsync(cancellationToken);
      var apiKeysTask = settingsService.FetchApiKeysAsync(cancellationToken);
      var sessionsTask = settingsService.FetchAuthSessionsAsync(cancellationToken);
      var membershipPlansTask = settingsService.FetchMembershipPlansAsync(cancellationToken);
      var membershipTask = settingsService.FetchMembershipAsync(cancellationToken);
      var pushSubscriptionsTask = settingsService.FetchPushSubscriptionsAsync(cancellationToken);
      var dataRequestTask = settingsService.FetchUserDataRequestAsync(userIdOrSlug, cancellationToken);

      await Task.WhenAll(
          profileTask,
          profileLinksTask,
          apiKeysTask,
          sessionsTask,
          membershipPlansTask,
          membershipTask,
          pushSubscriptionsTask,
          dataRequestTask).ConfigureAwait(true);
      if (!IsCurrentSettingsLoad(generation)) return;

      PrivacySelections = BuildSelections(user);
      PrivacyToggles = BuildToggles(user);

      var profile = (await profileTask.ConfigureAwait(true)).Profile;
      ProfileMarkdown = profile.Markdown ?? string.Empty;
      ProfileSummary = ProfileMarkdown.Length == 0
          ? localization.Localize(UiMessageKey.NativeDotnetProfileNoProfileBio)
          : localization.Format(
              UiMessageKey.NativeDotnetSettingsCharacterCount,
              ("count", ProfileMarkdown.Length));

      ProfileLinks = (await profileLinksTask.ConfigureAwait(true)).Results;
      var apiKeysResponse = await apiKeysTask.ConfigureAwait(true);
      var sessionsResponse = await sessionsTask.ConfigureAwait(true);
      ReplaceApiKeyPage(apiKeysResponse);
      ReplaceSessionPage(sessionsResponse);
      ApiKeySecret = null;

      var plansResponse = await membershipPlansTask.ConfigureAwait(true);
      var plans = plansResponse.Plans;
      Membership = (await membershipTask.ConfigureAwait(true))?.Membership;
      UpdateMembershipSummary();
      MembershipPlanOptions = BuildMembershipPlanOptions(plans, Membership, plansResponse.BenefitCatalog);

      ReplacePushSubscriptionPage(await pushSubscriptionsTask.ConfigureAwait(true));

      DataRequest = await dataRequestTask.ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      if (IsCurrentSettingsLoad(generation)) ErrorMessage = ex.Message;
    }
    finally
    {
      if (IsCurrentSettingsLoad(generation)) IsLoading = false;
    }
  }
}
