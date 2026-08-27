using Microsoft.Maui.Controls;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed partial class MemberAppealsPageTests
{
  [Fact]
  public async Task LocaleChangeRebuildsDirectCopyWithoutLosingTheDraft()
  {
    using var controller = new UiLocaleController(new Languages());
    using var localization = UiCopy.PushLocalization(new UiLocalization(controller));
    var page = new MemberAppealsPage(
        new MemberAppealsViewModel(
            new MemberService(),
            new NavigationViewer(true, [], IdentityId: "user-1"),
            MemberAppealsRoute.Warnings),
        new TokenProvider(),
        controller);
    await page.ReloadAsync();
    Find<Button>(page, "member-appeal-file-warning:warning-1").SendClicked();
    Find<Picker>(page, "member-appeal-reason").SelectedIndex = 4;
    Find<Editor>(page, "member-appeal-details").Text = "Draft details";
    var englishPickerTitle = Find<Picker>(page, "member-appeal-reason").Title;

    controller.ApplySavedLocale("fr");
    Assert.NotEqual(englishPickerTitle, Find<Picker>(page, "member-appeal-reason").Title);
    Assert.Equal(4, Find<Picker>(page, "member-appeal-reason").SelectedIndex);
    Assert.Equal("Draft details", Find<Editor>(page, "member-appeal-details").Text);
  }

  [Fact]
  public void ReplacementDisposesLocaleSubscriptionButSameReferenceDoesNot()
  {
    using var controller = new UiLocaleController(new Languages());
    var page = new MemberAppealsPage(
        new MemberAppealsViewModel(
            new MemberService(),
            new NavigationViewer(true, [], IdentityId: "user-1"),
            MemberAppealsRoute.Warnings),
        new TokenProvider(),
        controller);
    var content = new ShellContent { Content = page };
    var englishContent = page.Content;

    ModerationDisputesPageLifetime.Replace(content, page);
    controller.ApplySavedLocale("es");
    var spanishContent = page.Content;
    ModerationDisputesPageLifetime.Replace(content, new ContentPage());
    controller.ApplySavedLocale("fr");

    Assert.NotSame(englishContent, spanishContent);
    Assert.Same(spanishContent, page.Content);
  }
}
