using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Localization;

public sealed class NativeLocalizationSurfaceMatrixTests
{
  public static TheoryData<string, string, string, string, string, string, string> SurfaceCopy =>
      new()
      {
        {
          "en",
          "UI locale",
          "No referral links loaded",
          "View comment",
          "Title is required.",
          "Error",
          "Cancel"
        },
        {
          "es",
          "Idioma de la interfaz",
          "No hay enlaces de recomendación cargados",
          "Ver comentario",
          "Se requiere un título.",
          "Error",
          "Cancelar"
        },
        {
          "fr",
          "Langue de l’interface",
          "Aucun lien de parrainage chargé",
          "Voir le commentaire",
          "Le titre est requis.",
          "Erreur",
          "Annuler"
        },
        {
          "pt",
          "Idioma da interface",
          "Não há ligações de referência carregadas",
          "Ver comentário",
          "O título é obrigatório.",
          "Erro",
          "Cancelar"
        },
      };

  [Theory]
  [MemberData(nameof(SurfaceCopy))]
  public void GeneratedResourcesCoverNativeSurfaceKindsAndRefreshLive(
      string locale,
      string expectedSettings,
      string expectedCollection,
      string expectedDetail,
      string expectedValidation,
      string expectedEmptyError,
      string expectedCancel)
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);

    controller.ApplySavedLocale(locale);

    Assert.Equal(expectedSettings, localization.Localize(UiMessageKey.NativeDotnetSettingsUiLocale));
    Assert.Equal(expectedCollection, localization.Localize(UiMessageKey.NativeDotnetReferralLinksEmpty));
    Assert.Equal(expectedDetail, localization.Localize(UiMessageKey.NativeDotnetPostsViewComment));
    Assert.Equal(expectedValidation, localization.Localize(UiMessageKey.NativeDotnetPostsTitleRequired));
    Assert.Equal(expectedEmptyError, localization.Localize(UiMessageKey.NativeDotnetCsharpError));
    Assert.Equal(expectedCancel, localization.Localize(UiMessageKey.CommonCancel));
  }

  [Theory]
  [InlineData("en", 0, "Image 1")]
  [InlineData("es", 1, "Imagen 2")]
  [InlineData("fr", 4, "Image 5")]
  [InlineData("pt", 9, "Imagem 10")]
  public void ImageDraftLabelsUseTheGeneratedPluralDescriptor(
      string locale,
      int orderIndex,
      string expected)
  {
    var localization = new UiLocalization(
        new UiLocaleController(new StubDeviceLanguageProvider(locale)));

    var draft = new PostComposeImageDraft(
        "image-1",
        orderIndex,
        Localization: localization);

    Assert.Equal(expected, draft.DisplayLabel);
  }

  [Fact]
  public void StructuralPresentationValuesUseExplicitInvariantTypes()
  {
    Assert.Equal("Question *", UiInvariantText.RequiredMarker.AppendTo("Question"));
    Assert.Equal("0:00", UiInvariantText.ZeroDuration.Value);
    Assert.Equal("--:--", UiInvariantText.UnknownDuration.Value);
    Assert.Equal("@alice", UiUserHandle.FromUsername(" alice ").Value);
    Assert.Equal(string.Empty, UiUserHandle.FromUsername(" ").Value);
    Assert.Equal("Gmail SMTP", UiExternalProviderText.GmailSmtp.Value);
    Assert.Equal("SES", UiExternalProviderText.AmazonSes.Value);
    Assert.Equal("answer", UiUserInputText.FromValue("answer").Value);
    Assert.Equal("42", UiUserInputText.FromValue(42).Value);
    Assert.Equal(string.Empty, UiUserInputText.FromValue(null).Value);
    Assert.Equal(
        "https://example.com/watch?v=1",
        UiExternalContentText.FromUri(new Uri("https://example.com/watch?v=1")).Value);
    Assert.Equal(string.Empty, UiExternalContentText.FromUri(null).Value);
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
