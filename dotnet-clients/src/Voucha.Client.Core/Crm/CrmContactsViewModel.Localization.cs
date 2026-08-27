using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Crm;

public sealed partial class CrmContactsViewModel
{
  public IReadOnlyList<UiProtocolOption> StatusFilterOptions =>
      Options(
          UiTaxonomy.CrmStatuses,
          new UiProtocolOption(
              string.Empty,
              UiText.Localized(UiMessageKey.NativeDotnetCsharpCrmAnyStatus),
              localization));

  public IReadOnlyList<UiProtocolOption> VerticalFilterOptions =>
      Options(
          UiTaxonomy.CrmVerticals,
          new UiProtocolOption(
              string.Empty,
              UiText.Localized(UiMessageKey.NativeDotnetCsharpCrmAnyVertical),
              localization));

  public IReadOnlyList<UiProtocolOption> CreateVerticalOptions =>
      Options(
          UiTaxonomy.CrmVerticals,
          new UiProtocolOption(
              string.Empty,
              UiText.Localized(UiMessageKey.NativeDotnetCsharpCrmNoVertical),
              localization));

  public IReadOnlyList<UiProtocolOption> CreateContactTypeOptions =>
      Options(
          UiTaxonomy.CrmContactTypes,
          new UiProtocolOption(
              string.Empty,
              UiText.Localized(UiMessageKey.NativeDotnetCsharpCrmNoType),
              localization));

  public void OnUiLocaleChanged()
  {
    Contacts = Contacts.Select(row => row.WithLocalization(localization)).ToArray();
    SynchronizeContactPage();
    OnPropertyChanged(nameof(StatusFilterOptions));
    OnPropertyChanged(nameof(VerticalFilterOptions));
    OnPropertyChanged(nameof(CreateVerticalOptions));
    OnPropertyChanged(nameof(CreateContactTypeOptions));
  }

  public void Dispose() => localeSubscription?.Dispose();

  private UiProtocolOption[] Options(
      IReadOnlyList<UiProtocolOptionDefinition> definitions,
      UiProtocolOption first) =>
      [
        first,
        .. definitions.Select(definition =>
            UiProtocolOption.From(definition, localization)),
      ];
}
