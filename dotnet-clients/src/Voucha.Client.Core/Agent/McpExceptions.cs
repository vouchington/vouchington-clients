namespace Voucha.Client.Core.Agent;

public sealed class McpUnauthorizedException : Exception
{
  public McpUnauthorizedException() : base("MCP authorization is required.") { }
  public McpUnauthorizedException(string message) : base(message) { }
  public McpUnauthorizedException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class McpRateLimitException : Exception
{
  public McpRateLimitException() : base("MCP rate limit exceeded.") { }
  public McpRateLimitException(string message) : base(message) { }
  public McpRateLimitException(string message, Exception innerException) : base(message, innerException) { }
  public McpRateLimitException(TimeSpan? retryAfter) : this() => RetryAfter = retryAfter;

  public TimeSpan? RetryAfter { get; }
}

public sealed class McpRpcException : Exception
{
  public McpRpcException() { }
  public McpRpcException(string message) : base(message) { }
  public McpRpcException(string message, Exception innerException) : base(message, innerException) { }
  public McpRpcException(int code, string message) : base(message) => Code = code;

  public int Code { get; }
}
