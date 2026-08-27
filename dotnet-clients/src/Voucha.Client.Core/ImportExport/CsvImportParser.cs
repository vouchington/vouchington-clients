namespace Voucha.Client.Core.ImportExport;

internal static class CsvImportParser
{
  public static List<List<string>> Parse(string text)
  {
    var reader = new CsvReader(text);
    return reader.Parse();
  }

  private sealed class CsvReader
  {
    private readonly string csv;
    private readonly List<List<string>> records = [];
    private readonly List<string> record = [];
    private readonly System.Text.StringBuilder field = new();
    private bool inQuotes;
    private bool afterClosingQuote;
    private bool recordHasContent;
    private string? recordDelimiter;

    public CsvReader(string text) =>
        csv = text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;

    public List<List<string>> Parse()
    {
      for (var index = 0; index < csv.Length; index++)
      {
        var character = csv[index];
        if (inQuotes)
        {
          if (character != '"')
          {
            field.Append(character);
            continue;
          }
          if (index + 1 < csv.Length && csv[index + 1] == '"')
          {
            field.Append('"');
            index++;
            continue;
          }
          inQuotes = false;
          afterClosingQuote = true;
          continue;
        }

        var delimiter = ReadRecordDelimiter(ref index);
        if (delimiter is not null && IsActiveRecordDelimiter(delimiter))
        {
          AppendRecord();
          afterClosingQuote = false;
        }
        else if (afterClosingQuote)
        {
          ConsumeAfterClosingQuote(character, delimiter);
        }
        else
        {
          ConsumeUnquoted(character, delimiter);
        }
      }

      if (inQuotes) throw InvalidFormat();
      AppendRecord();
      if (records.Count > 0 && records.Any(row => row.Count != records[0].Count))
      {
        throw InvalidFormat();
      }
      return records;
    }

    private string? ReadRecordDelimiter(ref int index)
    {
      if (csv[index] == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
      {
        index++;
        return "\r\n";
      }
      return csv[index] is '\r' or '\n' ? csv[index].ToString() : null;
    }

    private bool IsActiveRecordDelimiter(string delimiter)
    {
      recordDelimiter ??= delimiter;
      return delimiter == recordDelimiter ||
          (delimiter == "\r\n" && recordDelimiter is "\r" or "\n");
    }

    private void ConsumeAfterClosingQuote(char character, string? delimiter)
    {
      if (delimiter is not null) throw InvalidFormat();
      if (character == ',')
      {
        AppendField();
        afterClosingQuote = false;
      }
      else if (character is not (' ' or '\t'))
      {
        throw InvalidFormat();
      }
    }

    private void ConsumeUnquoted(char character, string? delimiter)
    {
      if (delimiter is not null)
      {
        field.Append(delimiter);
      }
      else if (character == ',')
      {
        AppendField();
      }
      else if (character == '"' && string.IsNullOrWhiteSpace(field.ToString()))
      {
        field.Clear();
        inQuotes = true;
        recordHasContent = true;
      }
      else if (character == '"')
      {
        throw InvalidFormat();
      }
      else
      {
        field.Append(character);
        if (!char.IsWhiteSpace(character)) recordHasContent = true;
      }
    }

    private void AppendField()
    {
      record.Add(field.ToString().Trim());
      field.Clear();
      recordHasContent = true;
    }

    private void AppendRecord()
    {
      record.Add(field.ToString().Trim());
      field.Clear();
      if (recordHasContent) records.Add([.. record]);
      record.Clear();
      recordHasContent = false;
    }

    private static InvalidDataException InvalidFormat() => new("Invalid CSV format.");
  }
}
