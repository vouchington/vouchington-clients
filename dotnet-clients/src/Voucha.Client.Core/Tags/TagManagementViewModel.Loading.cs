using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Tags;

public sealed partial class TagManagementViewModel
{
  private async Task LoadEntityContextAsync(TagManagementRouteContext route, CancellationToken cancellationToken)
  {
    switch (route.EntityType)
    {
      case "post":
        var postResponse = await client.FetchPostAsync(route.EntityIdOrSlug, cancellationToken).ConfigureAwait(true);
        EntityLabel = postResponse.Post.Title;
        Context = route with { EntityIdOrSlug = postResponse.Post.Id };
        break;
      case "topic":
        var topicResponse = await client.FetchTopicAsync(route.EntityIdOrSlug, cancellationToken).ConfigureAwait(true);
        EntityLabel = topicResponse.Topic.Name;
        Context = route with { EntityIdOrSlug = topicResponse.Topic.Id, TopicType = topicResponse.Topic.TopicType };
        Tabs = TabsFor(Context);
        SelectedTab = SelectRequestedTab(route.ObjectType);
        break;
      case "rss_feed_item":
        var itemResponse = await client.FetchRssFeedItemAsync(route.EntityIdOrSlug, cancellationToken).ConfigureAwait(true);
        EntityLabel = itemResponse.RssFeedItem.Title ?? itemResponse.RssFeedItem.Data?.Title ?? itemResponse.RssFeedItem.Id;
        Context = route with { EntityIdOrSlug = itemResponse.RssFeedItem.Id };
        break;
      case "user":
        EntityLabel = route.EntityIdOrSlug;
        break;
      default:
        EntityLabel = route.EntityIdOrSlug;
        break;
    }

    RefreshTitle();
  }

  private async Task LoadSelectedTabAsync(CancellationToken cancellationToken)
  {
    if (Context is null || SelectedTab is null)
    {
      ClearResults();
      return;
    }

    var response = await client.FetchEntityRelationsAsync(
        new EntityRelationsRequest(Context.EntityType, Context.EntityIdOrSlug, SelectedTab.Predicate, SelectedTab.ObjectType, PositiveNetVoteScore: false, Limit: 25),
        cancellationToken).ConfigureAwait(true);
    ReplaceRelationPage(response);

    PublisherTypes = SelectedTab.Value == "publisher_type"
        ? (await client.FetchPublisherTypesAsync(cancellationToken).ConfigureAwait(true)).PublisherTypes
        : Context.EntityType == "user"
            ? (await client.FetchUserTagsAsync(cancellationToken).ConfigureAwait(true)).UserTags
            : [];

    if (Context.EntityType == "user")
    {
      var existingIds = Relations.Select(relation => relation.Subtitle).ToHashSet(StringComparer.Ordinal);
      SearchResults = PublisherTypes
          .Where(topic => !existingIds.Contains(topic.Id))
          .Select(topic => new TagSearchResultRow(topic.Id, topic.Label, topic.Slug))
          .ToArray();
    }
  }

  private async Task<IReadOnlyList<TagSearchResultRow>> SearchResultsAsync(
      string query,
      TagRelationTab tab,
      CancellationToken cancellationToken) =>
      tab.ObjectType switch
      {
        "topic" => (await client.SearchTopicsAsync(new SearchTopicsRequest(query), cancellationToken).ConfigureAwait(true)).Topics
            .Values.Select(topic => new TagSearchResultRow(topic.Id, topic.Name, topic.Slug)).ToArray(),
        "post" => (await client.SearchPostsAsync(query, 10, cancellationToken).ConfigureAwait(true)).Posts
            .Values.Select(post => new TagSearchResultRow(post.Id, post.Title ?? post.Slug ?? post.Id, post.PostType)).ToArray(),
        "url" => (await client.SearchUrlsAsync(query: query, limit: 10, cancellationToken: cancellationToken).ConfigureAwait(true)).Results
            .Select(url => new TagSearchResultRow(url.Id, url.UrlValue, url.Hostname?.HostnameValue)).ToArray(),
        _ => Array.Empty<TagSearchResultRow>(),
      };

  private TagRelationTab[] TabsFor(TagManagementRouteContext route) =>
      (route.EntityType switch
      {
        "post" => TagRelationConfigs.PostTagTabs,
        "rss_feed_item" => [new TagRelationTab(UiMessageKey.NativeDotnetTagManagementCategories, "topic", "category", "topic")],
        "user" => [new TagRelationTab(UiMessageKey.NativeDotnetTagManagementUserTags, "topic", "category", "topic")],
        "topic" when route.TopicType == "rss_feed" => TagRelationConfigs.TopicTagTabs,
        "topic" => TagRelationConfigs.GetTopicTagTabsForTopicType(route.TopicType),
        _ => Array.Empty<TagRelationTab>(),
      }).Select(tab => tab.WithLocalization(localization)).ToArray();

  private void RefreshTitle() =>
      Title = Context?.EntityType == "user"
          ? localization.Localize(UiMessageKey.NativeDotnetTagManagementManageUserTags)
          : SelectedTab?.Label
              ?? (Tabs.Count == 0 ? Context?.ObjectType : null)
              ?? localization.Localize(UiMessageKey.NativeDotnetCsharpTagsTags);

  private void ClearResults()
  {
    ResetRelationPagination();
    ClearSearch();
    PublisherTypes = [];
  }

  private void ClearSearch()
  {
    SearchResults = [];
    SearchQuery = string.Empty;
  }

  private static string GetRelationTitle(EntityRelation relation) =>
      TryGetDisplayTitle(relation.ObjectData) ?? relation.ObjectId ?? relation.Id;

  private static string? TryGetDisplayTitle(object? objectData) =>
      objectData switch
      {
        JsonElement element => TryGetDisplayTitle(element),
        string value when !string.IsNullOrWhiteSpace(value) => value,
        _ => null,
      };

  private static string? TryGetDisplayTitle(JsonElement objectData)
  {
    if (objectData.ValueKind == JsonValueKind.String)
    {
      var title = objectData.GetString();
      return string.IsNullOrWhiteSpace(title) ? null : title;
    }

    if (objectData.ValueKind != JsonValueKind.Object)
    {
      return null;
    }

    foreach (var propertyName in new[] { "name", "title", "url" })
    {
      if (objectData.TryGetProperty(propertyName, out var property) &&
          property.ValueKind == JsonValueKind.String)
      {
        var title = property.GetString();
        if (!string.IsNullOrWhiteSpace(title))
        {
          return title;
        }
      }
    }

    return null;
  }
}
