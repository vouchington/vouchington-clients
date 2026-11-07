import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityAgentPromptList: View {
    @Environment(\.locale) private var nativeUiLocale
    @Bindable var viewModel: CommunityAgentPromptWorkspace

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.pagination.items) { prompt in
                Text(verbatim: UiMessages.string(.verbatim(prompt.prompt), locale: nativeUiLocale))
            }
            if let notice = viewModel.notice {
                Text(verbatim: UiMessages.string(notice, locale: nativeUiLocale))
                    .foregroundStyle(.red)
            }
            HybridPaginationControl(
                hasMore: viewModel.pagination.hasMore,
                isLoading: viewModel.pagination.isLoading,
                hasError: viewModel.pagination.lastError != nil,
                isDisabled: viewModel.isCreating
            ) {
                await viewModel.loadMore()
            }
        }
        .task { await viewModel.reload() }
        .onDisappear { viewModel.invalidate() }
    }
}
