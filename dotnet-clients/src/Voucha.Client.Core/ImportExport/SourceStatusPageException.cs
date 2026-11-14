namespace Voucha.Client.Core.ImportExport;

public sealed class SourceStatusPageException : Exception
{
  public SourceStatusPageException() { }
  public SourceStatusPageException(string message) : base(message) { }
  public SourceStatusPageException(string message, Exception innerException) : base(message, innerException) { }
}
