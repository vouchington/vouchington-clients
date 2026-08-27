using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Api;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class OmnisearchPage : ContentPage
{
  private readonly OmnisearchViewModel viewModel;
  private readonly ITurnstileTokenProvider turnstileTokenProvider;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly Func<FediverseProvider, string?, Task>? replaceFediverseRoute;

  public OmnisearchPage(
      OmnisearchViewModel viewModel,
      ITurnstileTokenProvider turnstileTokenProvider,
      EmailVerificationRecoveryCoordinator emailRecovery,
      NavigationIntentViewModel? navigationIntent,
      Func<FediverseProvider, string?, Task>? replaceFediverseRoute = null)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.turnstileTokenProvider = turnstileTokenProvider;
    this.emailRecovery = emailRecovery;
    this.replaceFediverseRoute = replaceFediverseRoute;
    BindingContext = new OmnisearchPageBinding(viewModel, navigationIntent?.Groups ?? []);
  }

  private async void OnSearchButtonPressed(object? sender, EventArgs e)
  {
    await viewModel.SearchAsync();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadRouteContextAsync();
    await viewModel.SearchInitialQueryAsync();
  }

  public Task ApplyFediverseRouteAsync(NativeRouteMatch match) =>
      viewModel.ApplyFediverseSearchQueryAsync(
          match.QueryValue("q", "query"),
          match.QueryValue("provider"));

  private Task SelectProviderAsync(FediverseProvider provider) => replaceFediverseRoute is null
      ? viewModel.SelectFediverseProviderAsync(provider)
      : replaceFediverseRoute(provider, viewModel.Query);

  private async Task SelectCheckedProviderAsync(FediverseProvider provider, CheckedChangedEventArgs e)
  {
    if (!FediverseProviderSelection.ShouldRoute(
        e.Value,
        viewModel.SelectedFediverseProvider,
        provider)) return;
    await SelectProviderAsync(provider);
  }

  private async void OnAllProvidersChecked(object? sender, CheckedChangedEventArgs e)
  {
    await SelectCheckedProviderAsync(FediverseProvider.All, e);
  }

  private async void OnPeerTubeChecked(object? sender, CheckedChangedEventArgs e)
  {
    await SelectCheckedProviderAsync(FediverseProvider.PeerTube, e);
  }

  private async void OnMastodonChecked(object? sender, CheckedChangedEventArgs e)
  {
    await SelectCheckedProviderAsync(FediverseProvider.Mastodon, e);
  }

  private async void OnLemmyChecked(object? sender, CheckedChangedEventArgs e)
  {
    await SelectCheckedProviderAsync(FediverseProvider.Lemmy, e);
  }

  private async void OnBlueskyChecked(object? sender, CheckedChangedEventArgs e)
  {
    await SelectCheckedProviderAsync(FediverseProvider.Bluesky, e);
  }

  private async void OnSearchSectionClicked(object? sender, EventArgs e)
  {
    await viewModel.SearchAsync();
  }

  private async void OnDomainsSectionClicked(object? sender, EventArgs e)
  {
    await viewModel.LoadDomainsAsync();
  }

  private async void OnUrlsSectionClicked(object? sender, EventArgs e)
  {
    await viewModel.LoadUrlsAsync();
  }

  private async void OnOpenRowClicked(object? sender, EventArgs e)
  {
    if (sender is Button { BindingContext: OmnisearchResultRow row })
    {
      await RunActionAsync(async () =>
      {
        if (row.ExternalUrl is { } externalUrl)
        {
          if (!await Launcher.Default.OpenAsync(externalUrl))
          {
            throw new InvalidOperationException($"Could not open {externalUrl}.");
          }

          return;
        }

        await viewModel.OpenRowAsync(row);
      });
      return;
    }

    Debug.WriteLine($"OmnisearchPage: unexpected Open row sender {sender?.GetType().FullName ?? "<null>"}");
  }

  private async void OnChooseDomainVoteClicked(object? sender, EventArgs e)
  {
    var choice = await SemanticVoteActionSheet.ChooseSentimentAsync(this, viewModel.SelectedHostnameVote);
    if (choice is not null)
    {
      await RunActionAsync(() => viewModel.VoteSelectedHostnameAsync(choice));
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  private async void OnClearDomainVoteClicked(object? sender, EventArgs e)
  {
    await RunActionAsync(() => viewModel.VoteSelectedHostnameAsync(null));
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private async void OnMuteDomainClicked(object? sender, EventArgs e)
  {
    await RunActionAsync(() => viewModel.MuteSelectedHostnameAsync());
  }

  private async void OnBlockDomainClicked(object? sender, EventArgs e)
  {
    await RunActionAsync(() => viewModel.BlockSelectedHostnameAsync());
  }

  private async void OnTriggerCrawlClicked(object? sender, EventArgs e)
  {
    await RunActionAsync(() => viewModel.TriggerSelectedUrlCrawlAsync());
  }

  private async void OnReportDomainClicked(object? sender, EventArgs e)
  {
    await NativeReportPrompt.ShowAsync(
        this,
        UiText.Localized(
            UiMessageKey.NativeSwiftModerationReportsReportSubject,
            ("subject", UiText.Localized(UiMessageKey.NativeDotnetResidualDomain))),
        turnstileTokenProvider,
        async (reason, note, token) =>
        {
          var submitted = await viewModel.ReportSelectedHostnameAsync(reason, note, token);
          return submitted
              ? NativeReportSubmissionResult.Success
              : NativeReportSubmissionResult.NotSubmitted(
                  UiText.Localized(UiMessageKey.NativeSwiftModerationReportsActionUnavailable));
        });
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native page action failures are displayed in page state.")]
  private async Task RunActionAsync(Func<Task> action)
  {
    try
    {
      await action();
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      await DisplayAlertAsync(
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpActionFailed),
          ex.Message,
          UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk));
    }
  }

}
