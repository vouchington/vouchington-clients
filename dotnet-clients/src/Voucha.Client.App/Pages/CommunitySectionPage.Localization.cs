using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private readonly List<(Picker Picker, UiMessageKey[] Keys)> localizedPickers = [];

  private Picker LocalizedPicker(params UiMessageKey[] keys)
  {
    var picker = new Picker { SelectedIndex = 0 };
    localizedPickers.Add((picker, keys));
    RefreshLocalizedPicker(picker, keys);
    return picker;
  }

  private void RefreshLocalizedPickers()
  {
    foreach (var (picker, keys) in localizedPickers)
    {
      RefreshLocalizedPicker(picker, keys);
    }
  }

  private static void RefreshLocalizedPicker(Picker picker, UiMessageKey[] keys)
  {
    var selectedIndex = picker.SelectedIndex;
    picker.ItemsSource = keys.Select(UiCopy.Localize).ToArray();
    picker.SelectedIndex = Math.Clamp(selectedIndex, 0, keys.Length - 1);
  }
}
