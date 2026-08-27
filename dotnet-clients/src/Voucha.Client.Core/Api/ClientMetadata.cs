namespace Voucha.Client.Core.Api;

public enum ClientPlatform
{
  Windows,
  MacOS,
}

public sealed record ClientMetadata(
    ClientPlatform Platform,
    string AppVersion,
    string? SdkVersion = null);
