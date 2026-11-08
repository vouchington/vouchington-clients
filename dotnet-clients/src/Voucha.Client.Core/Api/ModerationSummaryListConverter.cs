using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

// Lists carry summaries. Only detail endpoints may deserialize the strict run-identity projection.
internal abstract class ModerationSummaryListConverter<TList, TRow, TSummary> : JsonConverter<TList>
    where TRow : class
{
  protected abstract string RowsProperty { get; }
  protected abstract TList Create(IReadOnlyList<TRow> rows, PageInfo pageInfo);
  protected abstract IReadOnlyList<TRow> Rows(TList value);
  protected abstract PageInfo Pagination(TList value);

  public override TList Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using var document = JsonDocument.ParseValue(ref reader);
    var root = document.RootElement;
    var summaries = root.GetProperty(RowsProperty).Deserialize<IReadOnlyList<TSummary>>(options)
        ?? throw new JsonException("A moderation summary list cannot be null.");
    var rowOptions = RowOptions(options);
    var rows = summaries.Select(summary => JsonSerializer.SerializeToElement(summary, options)
        .Deserialize<TRow>(rowOptions) ?? throw new JsonException("A moderation summary cannot be null.")).ToList();
    var pageInfo = root.GetProperty("page_info").Deserialize<PageInfo>(options)
        ?? throw new JsonException("A moderation summary list requires page_info.");
    return Create(rows, pageInfo);
  }

  public override void Write(Utf8JsonWriter writer, TList value, JsonSerializerOptions options)
  {
    var rowOptions = RowOptions(options);
    writer.WriteStartObject();
    writer.WritePropertyName(RowsProperty);
    writer.WriteStartArray();
    foreach (var row in Rows(value))
    {
      var summary = JsonSerializer.SerializeToElement(row, rowOptions).Deserialize<TSummary>(options);
      JsonSerializer.Serialize(writer, summary, options);
    }
    writer.WriteEndArray();
    writer.WritePropertyName("page_info");
    JsonSerializer.Serialize(writer, Pagination(value), options);
    writer.WriteEndObject();
  }

  private static JsonSerializerOptions RowOptions(JsonSerializerOptions options)
  {
    var inner = new JsonSerializerOptions(options);
    for (var index = inner.Converters.Count - 1; index >= 0; index--)
      if (inner.Converters[index] is ModerationRunIdentityConverter<TRow>) inner.Converters.RemoveAt(index);
    return inner;
  }
}

internal sealed class ModerationAppealListConverter :
    ModerationSummaryListConverter<ModerationAppealListResponse, ModerationAppeal, ModerationAppealSummary>
{
  protected override string RowsProperty => "appeals";
  protected override ModerationAppealListResponse Create(IReadOnlyList<ModerationAppeal> rows, PageInfo pageInfo) => new(rows, pageInfo);
  protected override IReadOnlyList<ModerationAppeal> Rows(ModerationAppealListResponse value) => value.Appeals;
  protected override PageInfo Pagination(ModerationAppealListResponse value) => value.PageInfo;
}

internal sealed class ModerationDisputeListConverter :
    ModerationSummaryListConverter<ModerationDisputeListResponse, ModerationDispute, ModerationDisputeSummary>
{
  protected override string RowsProperty => "disputes";
  protected override ModerationDisputeListResponse Create(IReadOnlyList<ModerationDispute> rows, PageInfo pageInfo) => new(rows, pageInfo);
  protected override IReadOnlyList<ModerationDispute> Rows(ModerationDisputeListResponse value) => value.Disputes;
  protected override PageInfo Pagination(ModerationDisputeListResponse value) => value.PageInfo;
}
