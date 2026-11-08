import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct CommunityAgentPromptList: View {
    @Environment(\.locale) private var nativeUiLocale
    @Bindable var viewModel: CommunityAgentPromptWorkspace

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(Array(viewModel.pagination.items.enumerated()), id: \.element.id) { index, prompt in
                Button {
                    viewModel.selectPrompt(prompt.id)
                } label: {
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(verbatim: UiMessages.string(.verbatim(prompt.prompt), locale: nativeUiLocale))
                        Text(verbatim: UiMessages.string(.joined([
                            .message(
                                .nativeSwiftCommunitiesOrderPosition,
                                numberParameters: ["position": Double(index + 1)]
                            ),
                            .verbatim(prompt.modelName), .verbatim(prompt.modelProvider)
                        ]), locale: nativeUiLocale)).font(Typography.caption)
                        LabeledContent(UiMessages.string(.nativeSwiftCommunitiesPromptId, locale: nativeUiLocale)) {
                            Text(verbatim: UiMessages.string(.protocolValue(prompt.id), locale: nativeUiLocale))
                        }
                        .font(Typography.caption)
                        if viewModel.selectedPromptId == prompt.id {
                            Image(systemName: "checkmark.circle.fill").accessibilityHidden(true)
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(Spacing.sm)
                    .background(viewModel.selectedPromptId == prompt.id ? Colors.primary.opacity(0.12) : .clear)
                }
                .buttonStyle(.plain)
                .accessibilityIdentifier("community-agent-prompt-\(prompt.id)")
                .accessibilityAddTraits(viewModel.selectedPromptId == prompt.id ? .isSelected : [])
            }
            if let notice = viewModel.notice {
                Text(verbatim: UiMessages.string(notice, locale: nativeUiLocale))
                    .foregroundStyle(.red)
            }
            HybridPaginationControl(
                hasMore: viewModel.pagination.hasMore,
                isLoading: viewModel.isLoading,
                hasError: viewModel.refreshError != nil || viewModel.pagination.lastError != nil,
                isDisabled: viewModel.isCreating
            ) {
                await viewModel.loadMore()
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel(UiMessages.string(.nativeSwiftCommunitiesPromptId, locale: nativeUiLocale))
        .task { await viewModel.reload() }
        .onDisappear { viewModel.invalidate() }
    }
}
