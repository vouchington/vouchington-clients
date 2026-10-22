namespace Voucha.Client.Core.SpendingCategories;

public sealed partial class SpendingCategoriesViewModel
{
  public void OnUiLocaleChanged()
  {
    CreateDraft.ApplyCulture(localization.Culture); EditDraft?.ApplyCulture(localization.Culture);
    OnPropertyChanged(nameof(CreateDraft)); OnPropertyChanged(nameof(EditDraft)); RebuildRows(); RebuildSearchRows(); OnPropertyChanged(nameof(Error)); OnPropertyChanged(nameof(LocalizedSearchErrorMessage));
    OnPropertyChanged(nameof(MonthlyFrequencyText)); OnPropertyChanged(nameof(AnnuallyFrequencyText)); OnPropertyChanged(nameof(DeleteTitle)); OnPropertyChanged(nameof(DeleteConfirmText)); OnPropertyChanged(nameof(DeleteCancelText));
  }
}
