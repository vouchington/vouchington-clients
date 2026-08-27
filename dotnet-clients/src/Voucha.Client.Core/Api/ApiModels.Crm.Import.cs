using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public sealed record CrmImportResponse(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("batch")] CrmImportBatch? Batch = null,
    [property: JsonPropertyName("error")] string? Error = null,
    [property: JsonPropertyName("validation")] CrmImportValidation? Validation = null);

public sealed record CrmImportBatch(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("import_type")] string ImportType,
    [property: JsonPropertyName("total_rows")] int TotalRows,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public sealed record CrmImportValidation(
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("rows")] IReadOnlyList<CrmImportValidationRow> Rows);

public sealed record CrmImportValidationRow(
    [property: JsonPropertyName("row_index")] int RowIndex,
    [property: JsonPropertyName("valid")] bool Valid,
    [property: JsonPropertyName("errors")] IReadOnlyList<string> Errors);
