import SwiftUI
import VouchaLocalization
import VouchaModels

struct CommunityBanManagementControls: View {
    @Environment(\.locale) var nativeUiLocale
    @Bindable var viewModel: CommunityDetailViewModel
    @State private var userId = ""
    @State private var showsLiftAllConfirmation = false

    var body: some View {
        VStack(alignment: .leading) {
            Button(UiMessages.string(.nativeSwiftCommunitiesBans, locale: nativeUiLocale)) {
                Task { await viewModel.loadBanHistory() }
            }
            banRows(viewModel.banHistory)
            if viewModel.banHistoryCursor != nil {
                Button(UiMessages.string(.nativeSwiftCommonLoadMore, locale: nativeUiLocale)) {
                    Task { await viewModel.loadBanHistory(more: true) }
                }
            }
            TextField(UiMessages.string(.nativeSwiftCommunitiesUserId, locale: nativeUiLocale), text: $userId)
            Button(UiMessages.string(UiMessageKey.moderationCommunityBansActive, locale: nativeUiLocale)) {
                Task { await viewModel.loadActiveBans(userId: userId) }
            }
            banRows(viewModel.activeBans)
            if viewModel.activeBansCursor != nil {
                Button(UiMessages.string(.nativeSwiftCommonLoadMore, locale: nativeUiLocale)) {
                    Task { await viewModel.loadActiveBans(userId: viewModel.activeBansUserId, more: true) }
                }
            }
            if !viewModel.activeBans.isEmpty {
                Button(
                    UiMessages.string(UiMessageKey.moderationCommunityBansLiftAll, locale: nativeUiLocale),
                    role: .destructive
                ) {
                    viewModel.requestLiftAllBans(userId: viewModel.activeBansUserId)
                    showsLiftAllConfirmation = true
                }
            }
            if let detail = viewModel.banDetail {
                Text(verbatim: detail.reason ?? "")
            }
            if let error = viewModel.banError {
                Text(verbatim: UiMessages.string(error, locale: nativeUiLocale)).foregroundStyle(.red)
            }
        }
        .disabled(viewModel.isLoadingBans || viewModel.isLiftingBans)
        .confirmationDialog(
            UiMessages.string(UiMessageKey.moderationCommunityBansConfirmLiftAll, locale: nativeUiLocale),
            isPresented: $showsLiftAllConfirmation
        ) {
            Button(
                UiMessages.string(UiMessageKey.moderationCommunityBansLiftAll, locale: nativeUiLocale),
                role: .destructive
            ) {
                Task { await viewModel.confirmLiftAllBans() }
            }
        } message: {
            Text(verbatim: UiMessages.string(
                UiMessageKey.moderationCommunityBansConfirmLiftAllDescription,
                parameters: ["userLabel": viewModel.pendingLiftAllUserId ?? ""], locale: nativeUiLocale
            ))
        }
    }

    private func banRows(_ rows: [CommunityBan]) -> some View {
        ForEach(rows) { ban in
            HStack {
                Button {
                    Task { await viewModel.loadBanDetail(banId: ban.id) }
                } label: {
                    Text(verbatim: ban.id)
                }
                Text(verbatim: ban.userId)
                if ban.liftedAt == nil {
                    Button(UiMessages.string(.nativeSwiftCommunityActionsLiftBan, locale: nativeUiLocale)) {
                        Task { await viewModel.liftBan(banId: ban.id) }
                    }
                }
            }
        }
    }
}
