using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;

namespace Voucha.Client.App.Pages;

public sealed partial class ModerationReportsPage
{
  private static string StatusLabel(ModerationReportStatus value) => UiCopy.Localize(value switch
  {
    ModerationReportStatus.Pending => UiMessageKey.NativeSwiftModerationReportsPending,
    ModerationReportStatus.Reviewed => UiMessageKey.NativeSwiftModerationReportsReviewed,
    ModerationReportStatus.Actioned => UiMessageKey.NativeSwiftModerationReportsActioned,
    _ => UiMessageKey.NativeSwiftModerationReportsDismissed,
  });

  private static ModeOption[] ModeOptions() =>
  [
    new(UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsGrouped), ModerationReportsMode.Grouped),
    new(UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsFlat), ModerationReportsMode.Flat),
  ];

  private void ConfigurePickerLabels()
  {
    applyingPickerState = true;
    statusPicker.ItemsSource = Enum.GetValues<ModerationReportStatus>().Select(StatusLabel).ToArray();
    modePicker.ItemsSource = ModeOptions();
    sortPicker.ItemsSource =
    [
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsSeverity),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsMostReports),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsOldest),
      UiCopy.Localize(UiMessageKey.NativeSwiftModerationReportsNewest),
    ];
    statusPicker.SelectedIndex = (int)viewModel.Status;
    modePicker.SelectedIndex = ModeIndex(viewModel.Mode);
    sortPicker.SelectedIndex = SortIndex(viewModel.Sort);
    applyingPickerState = false;
  }

  private ModerationReportsMode SelectedMode() =>
      viewModel.IsStaff &&
      modePicker.SelectedIndex >= 0 &&
      modePicker.SelectedIndex < ModeOptions().Length
          ? ModeOptions()[modePicker.SelectedIndex].Mode
          : ModerationReportsMode.Flat;

  private static int ModeIndex(ModerationReportsMode value)
  {
    var index = Array.FindIndex(ModeOptions(), option => option.Mode == value);
    return index >= 0 ? index : 0;
  }

  private static int SortIndex(ModerationReportSort value) => value switch
  {
    ModerationReportSort.Severity => 0,
    ModerationReportSort.MostReported => 1,
    ModerationReportSort.CreatedAtAsc => 2,
    _ => 3,
  };
}
