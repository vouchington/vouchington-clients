import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct CommunityAutomodWorkspaceView: View {
    @Environment(\.locale)
    private var locale
    @State
    var viewModel: CommunityAutomodWorkspaceViewModel
    var onNavigate: (String) -> Void = { _ in }

    var body: some View {
        if viewModel.canModerate {
            VStack(alignment: .leading, spacing: Spacing.sm) {
                CommunityAutomodActionForm(viewModel: viewModel)
                Text(UiMessages.string(
                    .extractedCommunitiesCommunityAutomodFlagsPanelAutomodFlagsB5fc56db,
                    locale: locale
                ))
                .font(Typography.headline)
                .accessibilityAddTraits(.isHeader)
                Text(UiMessages.string(
                    .extractedCommunitiesCommunityAutomodFlagsPanelPostsAutomodFlaggedForModeratorReview90005619,
                    locale: locale
                ))
                .foregroundStyle(.secondary)
                if let notice = viewModel.notice {
                    Text(UiMessages.string(notice, locale: locale))
                        .accessibilityIdentifier("community-automod-notice")
                }
                if let error = viewModel.mutationError {
                    Text(UiMessages.string(error, locale: locale))
                        .foregroundStyle(.red)
                        .accessibilityIdentifier("community-automod-mutation-error")
                }
                ForEach(viewModel.pagination.items) { entry in
                    flagCard(entry)
                }
                if viewModel.pagination.isLoading { ProgressView() }
                if viewModel.pagination.lastError != nil {
                    Text(UiMessages.string(.nativeSwiftEmptyStateUnableToLoad, locale: locale))
                    Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                        Task { await viewModel.loadNextPage() }
                    }
                    .disabled(viewModel.pagination.isLoading)
                }
                HybridPaginationControl(
                    hasMore: viewModel.pagination.hasLoadedPage && viewModel.pagination.hasMore,
                    isLoading: viewModel.pagination.isLoading,
                    hasError: viewModel.pagination.lastError != nil,
                    accessibilityIdentifier: "community-automod-flags-pagination"
                ) {
                    await viewModel.loadNextPage()
                }
            }
            .task {
                if !viewModel.pagination.hasLoadedPage { await viewModel.loadNextPage() }
            }
        }
    }

    private func flagCard(_ entry: CommunityModerationQueueEntry) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            if entry.targetPath != nil {
                Button {
                    if let path = entry.targetPath { onNavigate(path) }
                } label: {
                    Text(verbatim: UiMessages.string(.userContent(entry.targetLabel ?? entry.entityId), locale: locale))
                }
                .disabled(entry.targetPath == nil || !entry.targetAvailable)
                .accessibilityIdentifier("community-automod-post-\(entry.id)")
            }
            if let content = entry.targetContent {
                Text(verbatim: UiMessages.string(.userContent(content.text), locale: locale))
                    .authoredContentLanguage(
                        declared: content.declaredLanguage,
                        detected: content.linguaRsDetectedLanguage
                    )
            }
            if let reason = entry.reason ?? entry.flaggedReason, !reason.isEmpty {
                Text(verbatim: UiMessages.string(.userContent(reason), locale: locale))
            }
            Button(UiMessages.string(.extractedCommunitiesCommunityAutomodFlagsPanelDismiss48845bff, locale: locale)) {
                Task { await viewModel.dismiss(entry) }
            }
            .disabled(entry.automodFlagPostId == nil || viewModel.dismissingPostIds
                .contains(entry.automodFlagPostId ?? ""))
            .accessibilityIdentifier("community-dismiss-automod-\(entry.id)")
        }
        .padding(Spacing.sm)
    }
}
