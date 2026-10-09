using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class OAuthGrantRevocationConfirmationTests
{
  [Fact]
  public async Task CancelKeepsTheGrantAndConfirmRevokesTheSelectedGrant()
  {
    var localization = UiLocalization.English;
    var prompts = new List<(string Title, string Message, string Accept, string Cancel)>();
    var revoked = new List<string>();
    var shouldConfirm = false;

    Task<bool> Confirm(string title, string message, string accept, string cancel)
    {
      prompts.Add((title, message, accept, cancel));
      return Task.FromResult(shouldConfirm);
    }

    Task Revoke(string id)
    {
      revoked.Add(id);
      return Task.CompletedTask;
    }

    await OAuthGrantRevocationConfirmation.RunAsync("grant-one", "Agent one", localization, Confirm, Revoke);
    Assert.Empty(revoked);
    Assert.Equal(("Revoke", "Revoke access to Agent one?", "Revoke", "Cancel"), Assert.Single(prompts));

    shouldConfirm = true;
    await OAuthGrantRevocationConfirmation.RunAsync("grant-two", "Agent two", localization, Confirm, Revoke);
    Assert.Equal(["grant-two"], revoked);
    Assert.Equal("Revoke access to Agent two?", prompts[1].Message);
  }
}
