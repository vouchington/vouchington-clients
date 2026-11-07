import SwiftUI
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct ReviewDisputeDetail: View {
    let viewModel: ReviewDisputesViewModel
    let id: String

    var body: some View {
        ScrollView {
            if viewModel.isLoadingDetail {
                ProgressView().accessibilityIdentifier("dispute-detail-loading")
            } else if let error = viewModel.detailError {
                StaffDetailError(error: error) { Task { await viewModel.loadDetail(id: id) } }
            } else if let dispute = viewModel.detailReviewDispute, dispute.id == id {
                VStack {
                    if let error = viewModel.errorMessage { StaffDetailErrorText(error: error) }
                    ReviewDisputeCard(viewModel: viewModel, dispute: dispute)
                }
            }
        }
        .padding()
        .safeAreaInset(edge: .top) { StaffDetailDismissButton() }
        .task { await viewModel.loadDetail(id: id) }
    }
}

struct ModerationAppealDetail: View {
    let viewModel: ModerationAppealsViewModel
    let id: String

    var body: some View {
        ScrollView {
            if viewModel.isLoadingDetail {
                ProgressView().accessibilityIdentifier("appeal-detail-loading")
            } else if let error = viewModel.detailError {
                StaffDetailError(error: error) { Task { await viewModel.loadDetail(id: id) } }
            } else if let appeal = viewModel.detailModerationAppeal, appeal.id == id {
                VStack {
                    if let error = viewModel.errorMessage { StaffDetailErrorText(error: error) }
                    ModerationAppealCard(viewModel: viewModel, appeal: appeal)
                }
            }
        }
        .padding()
        .safeAreaInset(edge: .top) { StaffDetailDismissButton() }
        .task { await viewModel.loadDetail(id: id) }
    }
}

struct StaffDetailError: View {
    @Environment(\.locale)
    private var locale
    let error: UiVerbatimText
    let retry: () -> Void

    var body: some View {
        VStack {
            Text(UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                .accessibilityIdentifier("staff-detail-error")
            Button(UiMessages.string(.nativeCommonRetry, locale: locale), action: retry)
        }
    }
}

struct StaffDetailDismissButton: View {
    @Environment(\.dismiss)
    private var dismiss
    @Environment(\.locale)
    private var locale

    var body: some View {
        HStack {
            Spacer()
            Button(UiMessages.string(.nativeCommonDone, locale: locale)) { dismiss() }
        }
        .padding()
    }
}

private struct StaffDetailErrorText: View {
    @Environment(\.locale)
    private var locale
    let error: UiVerbatimText

    var body: some View {
        Text(UiMessages.string(error, locale: locale)).foregroundStyle(.red)
    }
}
