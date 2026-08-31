using System.Text;

namespace Voucha.Client.Core.Api;

internal sealed class CappedSseLineReader(StreamReader reader, int maximumLineCharacters)
{
  private readonly char[] buffer = new char[4096];
  private int offset;
  private int length;
  private bool skipLineFeed;

  public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
  {
    var line = new StringBuilder();
    while (true)
    {
      if (offset == length)
      {
        length = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
        offset = 0;
        if (length == 0)
        {
          return line.Length == 0 ? null : line.ToString();
        }
      }

      var character = buffer[offset++];
      if (skipLineFeed)
      {
        skipLineFeed = false;
        if (character == '\n') continue;
      }
      if (character == '\r')
      {
        skipLineFeed = true;
        return line.ToString();
      }
      if (character == '\n') return line.ToString();
      if (line.Length == maximumLineCharacters) throw new ChatStreamFrameTooLargeException();
      line.Append(character);
    }
  }
}
