using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  private VouchaApiClient? userTrustClient;
  private ElectionVoteChoice? userTrustChoice;
  private int positiveSignalsFromFollowingCount;
  private int negativeSignalsFromFollowingCount;
  private bool isVotingUserTrust;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public ElectionVoteChoice? UserTrustChoice
  {
    get => userTrustChoice;
    private set
    {
      if (!SetProperty(ref userTrustChoice, value)) return;
      OnPropertyChanged(nameof(CanVoteUserTrust));
      OnPropertyChanged(nameof(CanClearUserTrustVote));
    }
  }

  public int PositiveSignalsFromFollowingCount
  {
    get => positiveSignalsFromFollowingCount;
    private set
    {
      if (SetProperty(ref positiveSignalsFromFollowingCount, value))
        OnPropertyChanged(nameof(LocalizedPositiveSignalsFromFollowing));
    }
  }

  public int NegativeSignalsFromFollowingCount
  {
    get => negativeSignalsFromFollowingCount;
    private set
    {
      if (SetProperty(ref negativeSignalsFromFollowingCount, value))
        OnPropertyChanged(nameof(LocalizedNegativeSignalsFromFollowing));
    }
  }

  public string LocalizedPositiveSignalsFromFollowing => localization.Format(
      UiMessageKey.NativeSwiftProfilePositiveSignalsFromPeopleYouFollow,
      ("count", PositiveSignalsFromFollowingCount));

  public string LocalizedNegativeSignalsFromFollowing => localization.Format(
      UiMessageKey.NativeSwiftProfileNegativeSignalsFromPeopleYouFollow,
      ("count", NegativeSignalsFromFollowingCount));

  public bool IsVotingUserTrust
  {
    get => isVotingUserTrust;
    private set => SetProperty(ref isVotingUserTrust, value);
  }

  public bool CanVoteUserTrust => CanCreateUserTrustVote || CanClearUserTrustVote;

  public bool CanCreateUserTrustVote =>
      CanActOnUser && IsSignedInViewer && currentViewerCanCastPublicVotes && !IsVotingUserTrust;

  public bool CanClearUserTrustVote =>
      CanActOnUser &&
      IsSignedInViewer &&
      UserTrustChoice is not null &&
      !currentViewerCanCastPublicVotes &&
      !IsVotingUserTrust;

  public async Task LoadUserTrustContextAsync(CancellationToken cancellationToken = default)
  {
    if (userTrustClient is null || User?.Id is not { Length: > 0 } userId) return;
    try
    {
      var context = await userTrustClient.FetchUserTrustContextAsync(userId, cancellationToken).ConfigureAwait(true);
      if (User?.Id != userId) return;
      PositiveSignalsFromFollowingCount = context.PositiveByFollowing?.Total ?? 0;
      NegativeSignalsFromFollowingCount = context.NegativeByFollowing?.Total ?? 0;
      UserTrustChoice = context.ElectionVote?.Choice;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      PositiveSignalsFromFollowingCount = 0;
      NegativeSignalsFromFollowingCount = 0;
      UserTrustChoice = null;
    }
  }

  public async Task VoteUserTrustAsync(ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    if ((choice is null ? !CanClearUserTrustVote : !CanCreateUserTrustVote) ||
        userTrustClient is null ||
        User?.Id is not { Length: > 0 } userId) return;
    if (choice is { } selected) ElectionVotePolicy.Sentiment.Require(selected);
    IsVotingUserTrust = true;
    ErrorMessage = null;
    var previousChoice = UserTrustChoice;
    UserTrustChoice = choice;
    try
    {
      var mutationSucceeded = await EmailVerificationGate.RunAsync(
          async () =>
          {
            if (choice is { } selectedChoice)
            {
              await userTrustClient.VoteUserTrustAsync(userId, selectedChoice, cancellationToken)
                  .ConfigureAwait(true);
            }
            else
            {
              await userTrustClient.ClearUserTrustVoteAsync(userId, cancellationToken)
                  .ConfigureAwait(true);
            }
            return true;
          },
          ex =>
          {
            UserTrustChoice = previousChoice;
            ErrorMessage = ex.Message;
            return false;
          }).ConfigureAwait(true);
      if (mutationSucceeded) ApplySuccessfulUserTrustVoteSideEffects(choice);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      UserTrustChoice = previousChoice;
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsVotingUserTrust = false;
      OnPropertyChanged(nameof(CanVoteUserTrust));
      OnPropertyChanged(nameof(CanCreateUserTrustVote));
      OnPropertyChanged(nameof(CanClearUserTrustVote));
    }
  }

  public void ConfigureUserTrustClient(VouchaApiClient client) =>
      userTrustClient = client ?? throw new ArgumentNullException(nameof(client));

  private void ApplySuccessfulUserTrustVoteSideEffects(ElectionVoteChoice? choice)
  {
    if (choice != ElectionVoteChoice.Disavow) return;
    IsFollowingUser = false;
    IsMutedUser = true;
  }
}
