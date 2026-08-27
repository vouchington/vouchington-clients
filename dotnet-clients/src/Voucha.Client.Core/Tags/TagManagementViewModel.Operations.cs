using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tags;

public sealed partial class TagManagementViewModel
{
  private const int UrlSearchMinimumQueryLength = 3;
  private bool tagLimitReached;

  public bool TagLimitReached
  {
    get => tagLimitReached;
    private set
    {
      tagLimitReached = value;
      OnPropertyChanged();
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native tag workflows surface API failures in view state.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading)
    {
      return;
    }

    if (Context is null)
    {
      ClearResults();
      return;
    }

    IsLoading = true;
    ErrorMessage = null;
    // A full reload (e.g. returning to this page after upgrading) must not keep hiding the add-tag
    // form behind a cap reached on a prior visit -- re-derive it from a fresh mutation.
    TagLimitReached = false;
    try
    {
      await LoadEntityContextAsync(Context, cancellationToken).ConfigureAwait(true);
      await LoadSelectedTabAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      ClearResults();
    }
    finally
    {
      IsLoading = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native tag workflows surface API failures in view state.")]
  public async Task SelectTabAsync(string value, CancellationToken cancellationToken = default)
  {
    if (IsLoading)
    {
      return;
    }

    if (Tabs.FirstOrDefault(tab => tab.Value == value) is not { } tab) return;
    IsLoading = true;
    SelectedTab = tab;
    ResetRelationPagination();
    ErrorMessage = null;
    // Each relation tab has its own tag-limit budget, so a cap reached on one tab must not keep
    // hiding the add-tag form on another.
    TagLimitReached = false;
    ClearSearch();
    try
    {
      await LoadSelectedTabAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      ClearResults();
    }
    finally
    {
      IsLoading = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native tag workflows surface API failures in view state.")]
  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    if (Context is null || SelectedTab is null) return;
    if (Context.EntityType == "user")
    {
      SearchResults = PublisherTypes
          .Where(topic => topic.Label.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || topic.Slug.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
          .Select(topic => new TagSearchResultRow(topic.Id, topic.Label, topic.Slug))
          .ToArray();
      return;
    }
    if (SelectedTab.Value == "publisher_type")
    {
      SearchResults = PublisherTypes
          .Where(topic => topic.Label.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) || topic.Slug.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
          .Select(topic => new TagSearchResultRow(topic.Id, topic.Label, topic.Slug))
          .ToArray();
      return;
    }

    var query = SearchQuery.Trim();
    if (query.Length == 0 || (SelectedTab.ObjectType == "url" && query.Length < UrlSearchMinimumQueryLength))
    {
      SearchResults = [];
      return;
    }

    IsSearching = true;
    ErrorMessage = null;
    try
    {
      SearchResults = await SearchResultsAsync(query, SelectedTab, cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
      SearchResults = [];
    }
    finally
    {
      IsSearching = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native tag workflows surface API failures in view state.")]
  public async Task AddTagAsync(string objectId, CancellationToken cancellationToken = default)
  {
    if (IsLoading || Context is null || SelectedTab is null || string.IsNullOrWhiteSpace(objectId)) return;
    IsLoading = true;
    ErrorMessage = null;
    TagLimitReached = false;
    try
    {
      await client.CreateEntityRelationAsync(
          new CreateEntityRelationRequest(Context.EntityType, Context.EntityIdOrSlug, SelectedTab.Predicate, SelectedTab.ObjectType, objectId.Trim()),
          cancellationToken).ConfigureAwait(true);
      await LoadSelectedTabAsync(cancellationToken).ConfigureAwait(true);
      ClearSearch();
    }
    catch (VouchaApiException ex) when (ex.IsTagLimitReached)
    {
      TagLimitReached = true;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Native tag workflows surface API failures in view state.")]
  public async Task VoteRelationAsync(string relationId, ElectionVoteChoice? choice, CancellationToken cancellationToken = default)
  {
    if (IsLoading || string.IsNullOrWhiteSpace(relationId)) return;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      if (choice is { } selected)
      {
        await client.VoteEntityRelationAsync(relationId, selected, cancellationToken).ConfigureAwait(true);
      }
      else
      {
        await client.ClearEntityRelationVoteAsync(relationId, cancellationToken).ConfigureAwait(true);
      }
      await LoadSelectedTabAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }
}
