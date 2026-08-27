using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.App;

namespace Voucha.Client.App.Controls;

public static class SemanticVoteActionSheet
{
  public static string ClearLabel => UiCopy.Localize(UiMessageKey.ExtractedVotesSemanticVoteClear);

  public static string SignInLabel => UiCopy.Localize(UiMessageKey.ExtractedVotesSemanticVoteSignIn);

  public static string Label(ElectionVoteChoice choice) => UiCopy.Localize(choice switch
  {
    ElectionVoteChoice.Vouch => UiMessageKey.ExtractedVotesSemanticVoteVouch,
    ElectionVoteChoice.Like => UiMessageKey.ExtractedVotesSemanticVoteLike,
    ElectionVoteChoice.Neutral => UiMessageKey.ExtractedVotesSemanticVoteNeutral,
    ElectionVoteChoice.Dislike => UiMessageKey.ExtractedVotesSemanticVoteDislike,
    ElectionVoteChoice.Disavow => UiMessageKey.ExtractedVotesSemanticVoteDisavow,
    ElectionVoteChoice.Support => UiMessageKey.ExtractedVotesSemanticVoteSupport,
    ElectionVoteChoice.Oppose => UiMessageKey.ExtractedVotesSemanticVoteOppose,
    ElectionVoteChoice.Confirm => UiMessageKey.ExtractedVotesSemanticVoteConfirm,
    ElectionVoteChoice.Dispute => UiMessageKey.ExtractedVotesSemanticVoteDispute,
    ElectionVoteChoice.Accurate => UiMessageKey.ExtractedVotesSemanticVoteAccurate,
    ElectionVoteChoice.Inaccurate => UiMessageKey.ExtractedVotesSemanticVoteInaccurate,
    _ => UiMessageKey.ExtractedVotesSemanticVoteVote,
  });

  public static Task<ElectionVoteChoice?> ChooseSentimentAsync(
      Page page,
      ElectionVoteChoice? current = null)
  {
    var allowed = current is null
        ? new[]
        {
          ElectionVoteChoice.Vouch,
          ElectionVoteChoice.Like,
          ElectionVoteChoice.Dislike,
          ElectionVoteChoice.Disavow,
        }
        : new[]
        {
          ElectionVoteChoice.Vouch,
          ElectionVoteChoice.Like,
          ElectionVoteChoice.Neutral,
          ElectionVoteChoice.Dislike,
          ElectionVoteChoice.Disavow,
        };
    return ChooseAsync(page, allowed);
  }

  public static Task<ElectionVoteChoice?> ChooseRecommendationAsync(Page page) => ChooseAsync(
      page,
      ElectionVoteChoice.Support,
      ElectionVoteChoice.Oppose);

  public static async Task<ElectionVoteChoice?> ChooseRelationAsync(Page page)
  {
    return await ChooseAsync(page, ElectionVoteChoice.Confirm, ElectionVoteChoice.Dispute).ConfigureAwait(true);
  }

  private static async Task<ElectionVoteChoice?> ChooseAsync(
      Page page,
      params ElectionVoteChoice[] allowedChoices)
  {
    var choices = allowedChoices.ToDictionary(Label, choice => choice, StringComparer.Ordinal);
    var selected = await page.DisplayActionSheet(
        UiCopy.Localize(UiMessageKey.ExtractedVotesSemanticVoteChoose),
        UiCopy.Localize(UiMessageKey.CommonCancel),
        null,
        choices.Keys.ToArray()).ConfigureAwait(true);
    return selected is not null && choices.TryGetValue(selected, out var choice) ? choice : null;
  }
}
