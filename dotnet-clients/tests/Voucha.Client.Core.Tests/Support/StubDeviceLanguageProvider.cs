// This file is compiled into both .NET test projects, so the alias is in scope at every call site.
global using StubDeviceLanguageProvider = Voucha.Client.Core.Tests.Support.StubDeviceLanguageProvider;

using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Tests.Support;

internal sealed class StubDeviceLanguageProvider(params string[] languages) : IDeviceLanguageProvider
{
  public IReadOnlyList<string> PreferredLanguages { get; } = languages;
}
