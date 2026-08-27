using System.Globalization;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public sealed class MauiDeviceLanguageProvider : IDeviceLanguageProvider
{
  public IReadOnlyList<string> PreferredLanguages =>
      DeviceLanguagePreferences.Ordered(
          PlatformPreferredLanguages(),
          CultureInfo.CurrentUICulture.Name);

  private static IReadOnlyList<string> PlatformPreferredLanguages()
  {
#if MACCATALYST
    return Foundation.NSLocale.PreferredLanguages;
#elif ANDROID
    var locales = Android.OS.LocaleList.GetAdjustedDefault();
    return Enumerable.Range(0, locales.Count)
        .Select(index => locales.Get(index)?.ToLanguageTag())
        .Where(language => !string.IsNullOrWhiteSpace(language))
        .Cast<string>()
        .ToArray();
#elif WINDOWS
    return Windows.System.UserProfile.GlobalizationPreferences.Languages;
#else
    return [];
#endif
  }
}
