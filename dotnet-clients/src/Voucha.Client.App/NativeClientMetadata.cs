using Microsoft.Maui.ApplicationModel;
using Voucha.Client.Core.Api;

namespace Voucha.Client.App;

internal static class NativeClientMetadata
{
  internal static ClientMetadata Current()
  {
    var platform = DeviceInfo.Platform == DevicePlatform.WinUI
        ? ClientPlatform.Windows
        : ClientPlatform.MacOS;
    var version = $"{AppInfo.VersionString}+{AppInfo.BuildString}";
    return new ClientMetadata(platform, version);
  }
}
