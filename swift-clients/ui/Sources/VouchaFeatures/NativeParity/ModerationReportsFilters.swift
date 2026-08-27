import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct ModerationReportsFilters: View {
    @Environment(\.locale)
    private var nativeUiLocale
    @Bindable
    var viewModel: ModerationReportsViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Picker(
                UiMessages.string(.nativeSwiftModerationReportsStatus, locale: nativeUiLocale),
                selection: statusBinding
            ) {
                ForEach(ModerationReportStatus.allCases, id: \.self) { status in
                    Text(UiMessages.string(status.titleKey, locale: nativeUiLocale)).tag(status)
                }
            }
            .pickerStyle(.segmented)

            if viewModel.viewerTier.isStaff {
                HStack {
                    Picker(
                        UiMessages.string(.nativeSwiftModerationReportsView, locale: nativeUiLocale),
                        selection: modeBinding
                    ) {
                        Text(UiMessages.string(
                            .nativeSwiftModerationReportsGrouped,
                            locale: nativeUiLocale
                        )).tag(ModerationReportsMode.grouped)
                        Text(UiMessages.string(
                            .nativeSwiftModerationReportsFlat,
                            locale: nativeUiLocale
                        )).tag(ModerationReportsMode.flat)
                    }
                    if viewModel.mode == .flat {
                        Picker(
                            UiMessages.string(.nativeSwiftModerationReportsSort, locale: nativeUiLocale),
                            selection: sortBinding
                        ) {
                            Text(UiMessages.string(
                                .nativeSwiftModerationReportsSeverity,
                                locale: nativeUiLocale
                            )).tag(ModerationReportSort.severity)
                            Text(UiMessages.string(
                                .nativeSwiftModerationReportsMostReports,
                                locale: nativeUiLocale
                            )).tag(ModerationReportSort.mostReported)
                            Text(UiMessages.string(
                                .nativeSwiftModerationReportsNewest,
                                locale: nativeUiLocale
                            )).tag(ModerationReportSort.createdAtDesc)
                            Text(UiMessages.string(
                                .nativeSwiftModerationReportsOldest,
                                locale: nativeUiLocale
                            )).tag(ModerationReportSort.createdAtAsc)
                        }
                    }
                }
            }
        }
        .disabled(viewModel.isLoading || viewModel.isQueueActionInProgress)
    }

    private var statusBinding: Binding<ModerationReportStatus> {
        Binding(get: { viewModel.status }, set: { value in
            Task { await viewModel.updateStatus(value) }
        })
    }

    private var modeBinding: Binding<ModerationReportsMode> {
        Binding(get: { viewModel.mode }, set: { value in
            Task { await viewModel.updateMode(value) }
        })
    }

    private var sortBinding: Binding<ModerationReportSort> {
        Binding(get: { viewModel.sort }, set: { value in
            Task { await viewModel.updateSort(value) }
        })
    }
}

extension ModerationReportStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .pending: .nativeSwiftModerationReportsPending
        case .reviewed: .nativeSwiftModerationReportsReviewed
        case .actioned: .nativeSwiftModerationReportsActioned
        case .dismissed: .nativeSwiftModerationReportsDismissed
        }
    }
}
