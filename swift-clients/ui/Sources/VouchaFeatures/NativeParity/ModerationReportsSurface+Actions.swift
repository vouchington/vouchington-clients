import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension ModerationReportsSurface {
    var bulkActions: some View {
        HStack {
            Button(UiMessages.string(
                .nativeSwiftModerationReportsDismissSelected,
                locale: nativeUiLocale
            )) { viewModel.pendingConfirmation = .bulkDismiss }
            if viewModel.viewerTier.canRemove {
                Button(UiMessages.string(
                    .nativeSwiftModerationReportsRemoveSelected,
                    locale: nativeUiLocale
                ), role: .destructive) { viewModel.pendingConfirmation = .bulkRemove }
            }
        }
        .disabled(viewModel.isQueueActionInProgress)
    }

    var selectedActionErrors: some View {
        ForEach(viewModel.selectedReportIds.sorted(), id: \.self) { reportId in
            if let error = viewModel.actionErrors[reportId] {
                Label(UiMessages.string(error, locale: nativeUiLocale), systemImage: "exclamationmark.triangle")
                    .foregroundStyle(Colors.negativeVote)
            }
        }
    }

    func run(_ value: ModerationReportsConfirmation) {
        viewModel.pendingConfirmation = nil
        Task {
            switch value {
            case .bulkDismiss: await viewModel.bulkDismissSelected()
            case .bulkRemove: await viewModel.bulkRemoveSelected()
            case let .clusterDismiss(id): await viewModel.dismissLoadedReports(in: id)
            case let .report(action, id): await viewModel.perform(action, reportId: id)
            }
        }
    }
}
