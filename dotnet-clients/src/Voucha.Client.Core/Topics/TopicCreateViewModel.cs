using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Topics;

public sealed class TopicCreateViewModel : ObservableObject
{
  private readonly ITopicsService topicsService;
  private string name = "";
  private string slug = "";
  private string topicType = "topic";
  private string markdown = "";
  private string? hostname;
  private bool isSaving;
  private string? errorMessage;

  public TopicCreateViewModel(ITopicsService topicsService) =>
      this.topicsService = topicsService ?? throw new ArgumentNullException(nameof(topicsService));

  public string Name
  {
    get => name;
    set => SetProperty(ref name, value);
  }

  public string Slug
  {
    get => slug;
    set => SetProperty(ref slug, value);
  }

  public string TopicType
  {
    get => topicType;
    set => SetProperty(ref topicType, value);
  }

  public string Markdown
  {
    get => markdown;
    set => SetProperty(ref markdown, value);
  }

  public string? Hostname
  {
    get => hostname;
    set => SetProperty(ref hostname, value);
  }

  public bool IsSaving
  {
    get => isSaving;
    private set => SetProperty(ref isSaving, value);
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public async Task<TopicMutationResponse> CreateAsync(CancellationToken cancellationToken = default)
  {
    IsSaving = true;
    ErrorMessage = null;
    try
    {
      return await topicsService
          .CreateTopicAsync(new CreateTopicRequest(Name, Slug, TopicType, Markdown, Hostname), cancellationToken)
          .ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      throw;
    }
    finally
    {
      IsSaving = false;
    }
  }
}
