using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Communities;

public sealed partial class CommunityActionViewModel
{
  public IReadOnlyList<CommunityApplicationQuestion> ApplicationQuestions
  {
    get => applicationQuestions;
    private set => SetProperty(ref applicationQuestions, value);
  }

  public IReadOnlyDictionary<string, object> ApplicationAnswers
  {
    get => applicationAnswers;
    private set => SetProperty(ref applicationAnswers, value);
  }

  public async Task LoadApplicationQuestionsAsync(CancellationToken cancellationToken = default)
  {
    if (kind != CommunityActionKind.Apply || string.IsNullOrWhiteSpace(CommunitySlug))
    {
      ApplicationQuestions = [];
      ApplicationAnswers = new Dictionary<string, object>();
      return;
    }

    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      var response = await service.FetchApplicationQuestionsAsync(CommunitySlug, cancellationToken).ConfigureAwait(true);
      ApplicationQuestions = response.Questions;
      ApplicationAnswers = response.Questions.ToDictionary(
          question => question.Id,
          question => ApplicationAnswers.TryGetValue(question.Id, out var answer) ? answer : "",
          StringComparer.Ordinal);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (HandleLoadException(ex))
    {
    }
  }

  public void SetApplicationAnswer(string questionId, string? answer)
  {
    if (string.IsNullOrWhiteSpace(questionId))
    {
      return;
    }

    ApplicationAnswers = new Dictionary<string, object>(ApplicationAnswers, StringComparer.Ordinal)
    {
      [questionId] = answer ?? "",
    };
  }
}
