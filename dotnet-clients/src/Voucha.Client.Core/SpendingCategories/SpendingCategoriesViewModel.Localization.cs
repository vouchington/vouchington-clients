namespace Voucha.Client.Core.SpendingCategories;

public sealed partial class SpendingCategoriesViewModel
{
  public void OnUiLocaleChanged()
  {
    CreateDraft.ApplyCulture(localization.Culture); EditDraft?.ApplyCulture(localization.Culture);
    Notify(nameof(CreateDraft)); Notify(nameof(EditDraft)); RebuildRows(); RebuildSearchRows(); Notify(nameof(Error)); Notify(nameof(LocalizedSearchErrorMessage));
    Notify(nameof(MonthlyFrequencyText)); Notify(nameof(AnnuallyFrequencyText)); Notify(nameof(DeleteTitle)); Notify(nameof(DeleteConfirmText)); Notify(nameof(DeleteCancelText));
  }
}
