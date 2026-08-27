using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public enum NewsFeedSourceType
{
  Article,
  Podcast,
  Video,
}

public static class NewsFeedSourceTypeExtensions
{
  public static string ApiValue(this NewsFeedSourceType sourceType) =>
      sourceType switch
      {
        NewsFeedSourceType.Article => "article",
        NewsFeedSourceType.Podcast => "podcast",
        NewsFeedSourceType.Video => "video",
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown news feed source type."),
      };

  public static string Label(this NewsFeedSourceType sourceType, IUiLocalization? localization = null) =>
      sourceType switch
      {
        NewsFeedSourceType.Article => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsArticle),
        NewsFeedSourceType.Podcast => L(localization).Localize(UiMessageKey.NativeDotnetNewsFeedsPodcast),
        NewsFeedSourceType.Video => L(localization).Localize(UiMessageKey.NativeDotnetResidualVideo),
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown news feed source type."),
      };

  public static string SourceLabel(this NewsFeedSourceType sourceType, IUiLocalization? localization = null) =>
      L(localization).Localize(sourceType switch
      {
        NewsFeedSourceType.Article => UiMessageKey.NativeDotnetNewsFeedsNewsSource,
        NewsFeedSourceType.Podcast => UiMessageKey.NativeDotnetNewsFeedsPodcastSource,
        NewsFeedSourceType.Video => UiMessageKey.NativeDotnetNewsFeedsVideoSource,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unknown news feed source type."),
      });

  public static NewsFeedSourceType? ParseApiValue(string? rawValue) =>
      rawValue switch
      {
        "article" => NewsFeedSourceType.Article,
        "podcast" => NewsFeedSourceType.Podcast,
        "video" => NewsFeedSourceType.Video,
        _ => null,
      };

  public static bool TryParseApiValue(string? rawValue, out NewsFeedSourceType sourceType)
  {
    var parsedValue = ParseApiValue(rawValue);
    sourceType = parsedValue.GetValueOrDefault();
    return parsedValue is not null;
  }

  private static IUiLocalization L(IUiLocalization? localization) => localization ?? UiLocalization.English;
}

public sealed class NewsFeedSourceTypeConverter : JsonConverter<NewsFeedSourceType?>
{
  public override NewsFeedSourceType? Read(
      ref Utf8JsonReader reader,
      Type typeToConvert,
      JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
    {
      return null;
    }

    return NewsFeedSourceTypeExtensions.ParseApiValue(reader.GetString());
  }

  public override void Write(Utf8JsonWriter writer, NewsFeedSourceType? value, JsonSerializerOptions options)
  {
    ArgumentNullException.ThrowIfNull(writer);

    if (value is null)
    {
      writer.WriteNullValue();
      return;
    }

    writer.WriteStringValue(value.Value.ApiValue());
  }
}
