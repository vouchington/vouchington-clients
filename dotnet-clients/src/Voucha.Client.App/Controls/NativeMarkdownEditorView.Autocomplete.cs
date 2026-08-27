using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Controls;

public sealed partial class NativeMarkdownEditorView
{
  // Caps fan-out from a single autocomplete lookup while still following page_info instead of
  // silently truncating at page one.
  private const int UserAutocompletePageLimit = 8;
  private const int UserAutocompleteMaxAdditionalPages = 4;

  private void ScheduleAutocomplete(string markdown)
  {
    autocompleteCancellation?.Cancel();
    autocompleteCancellation?.Dispose();

    if (!AutocompleteToken.TryParse(markdown, out var token))
    {
      autocompleteToken = null;
      suggestionsLayout.Children.Clear();
      suggestionsLayout.IsVisible = false;
      return;
    }

    autocompleteToken = token;
    var cancellation = new CancellationTokenSource();
    autocompleteCancellation = cancellation;
    _ = LoadAutocompleteAsync(token, cancellation.Token);
  }

  private async Task LoadAutocompleteAsync(AutocompleteToken token, CancellationToken cancellationToken)
  {
    try
    {
      await Task.Delay(180, cancellationToken).ConfigureAwait(true);
      var suggestions = await SuggestionsAsync(token, cancellationToken).ConfigureAwait(true);
      if (cancellationToken.IsCancellationRequested || autocompleteToken != token) return;
      RenderSuggestions(suggestions);
    }
    catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
    {
    }
    catch (Exception)
    {
      RenderSuggestions([]);
    }
  }

  private async Task<IReadOnlyList<AutocompleteSuggestion>> SuggestionsAsync(
      AutocompleteToken token,
      CancellationToken cancellationToken)
  {
    if (ApiClient is null) return [];

    return token.Marker switch
    {
      '@' => await MatchingUsersAsync(ApiClient, token.Query, cancellationToken).ConfigureAwait(true),
      '#' => (await ApiClient.SearchTopicsAsync(new SearchTopicsRequest(token.Query, Limit: 8), cancellationToken)
          .ConfigureAwait(true))
          .Results
          .Select(result => TopicSuggestion(result))
          .Where(suggestion => suggestion is not null)
          .Cast<AutocompleteSuggestion>()
          .ToArray(),
      '!' => (await ApiClient.SearchPostsAsync(token.Query, 8, cancellationToken).ConfigureAwait(true))
          .Results
          .Select(result => PostSuggestion(result))
          .Where(suggestion => suggestion is not null)
          .Cast<AutocompleteSuggestion>()
          .ToArray(),
      _ => [],
    };
  }

  // Follows page_info.has_next_page until there are enough matches, the result set is
  // exhausted, or the page-count safety bound is hit.
  private static async Task<IReadOnlyList<AutocompleteSuggestion>> MatchingUsersAsync(
      VouchaApiClient client, string query, CancellationToken cancellationToken)
  {
    var matches = new List<AutocompleteSuggestion>();
    string? after = null;
    for (var page = 0; page <= UserAutocompleteMaxAdditionalPages; page++)
    {
      var response = await client.SearchUsersAsync(
          new SearchUsersRequest(query, after, UserAutocompletePageLimit), cancellationToken).ConfigureAwait(true);
      matches.AddRange(response.Results
          .Where(user => !string.IsNullOrWhiteSpace(user.Username))
          .Select(user => new AutocompleteSuggestion($"@{user.Username}", $"@{user.Username}", user.Name)));
      if (matches.Count >= UserAutocompletePageLimit || !response.PageInfo.HasNextPage || response.PageInfo.EndCursor is not { } endCursor)
      {
        break;
      }

      after = endCursor;
    }

    return matches;
  }

  private static AutocompleteSuggestion? TopicSuggestion(EntityReference result)
  {
    var key = result.Slug ?? result.EntityId ?? result.Id;
    return string.IsNullOrWhiteSpace(key)
        ? null
        : new AutocompleteSuggestion($"#{key}", $"#{key}", result.Name);
  }

  private static AutocompleteSuggestion? PostSuggestion(EntityReference result)
  {
    var key = result.Slug ?? result.EntityId ?? result.Id;
    return string.IsNullOrWhiteSpace(key)
        ? null
        : new AutocompleteSuggestion($"!{key}", $"!{key}", result.Name);
  }

  private void RenderSuggestions(IReadOnlyList<AutocompleteSuggestion> suggestions)
  {
    suggestionsLayout.Children.Clear();
    foreach (var suggestion in suggestions)
    {
      var button = new Button
      {
        Text = suggestion.Detail is null
            ? suggestion.Label
            : UiCopy.Format(
                UiMessageKey.NativeDotnetCsharpEditorSuggestionWithDetail,
                ("label", UiText.Verbatim(suggestion.Label)),
                ("detail", UiText.Verbatim(suggestion.Detail))),
        HorizontalOptions = LayoutOptions.Fill,
      };
      button.Clicked += (_, _) => InsertSuggestion(suggestion);
      suggestionsLayout.Children.Add(button);
    }

    suggestionsLayout.IsVisible = editor.IsVisible && suggestionsLayout.Children.Count > 0;
  }

  private void InsertSuggestion(AutocompleteSuggestion suggestion)
  {
    if (autocompleteToken is null) return;
    var token = autocompleteToken.Value;
    Markdown = string.Concat(
        Markdown.AsSpan(0, token.Start),
        suggestion.Replacement,
        " ",
        Markdown.AsSpan(token.End));
    autocompleteToken = null;
    suggestionsLayout.Children.Clear();
    suggestionsLayout.IsVisible = false;
  }
}
