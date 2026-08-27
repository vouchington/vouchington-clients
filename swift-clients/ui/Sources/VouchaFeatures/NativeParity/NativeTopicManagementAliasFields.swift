import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

struct NativeTopicManagementAliasFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeTopicManagementViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack(spacing: Spacing.sm) {
                TextField(
                    UiMessages.string(.nativeSwiftTopicManagementFieldsAlias, locale: nativeUiLocale),
                    text: $viewModel.aliasDraft
                )
                .textFieldStyle(.roundedBorder)
                Button(UiMessages.string(.nativeSwiftTopicManagementFieldsAddAlias, locale: nativeUiLocale)) {
                    Task { await viewModel.addAlias() }
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.aliasDraft.trimmed.isEmpty || viewModel.isLoading)
            }
            if viewModel.aliases.isEmpty {
                EmptyStateView(
                    icon: "tag",
                    title: .message(.nativeSwiftEmptyStateNoAliases),
                    message: .message(.nativeSwiftEmptyStateAddAliasesMessage)
                )
            } else {
                aliasRows
            }
        }
    }

    private var aliasRows: some View {
        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.aliases, id: \.self) { alias in
                HStack {
                    Text(alias)
                    Spacer(minLength: 0)
                    Button {
                        Task { await viewModel.removeAlias(alias) }
                    } label: {
                        Image(systemName: "trash")
                    }
                    .buttonStyle(.plain)
                }
            }
            HybridPaginationControl(
                hasMore: viewModel.aliasesPagination.hasMore,
                isLoading: viewModel.aliasesPagination.isLoading,
                hasError: viewModel.aliasesPagination.lastError != nil,
                accessibilityIdentifier: "topic-aliases-pagination"
            ) {
                await viewModel.loadMoreAliases()
            }
        }
    }
}

struct NativeTopicManagementMergeFields: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    var viewModel: NativeTopicManagementViewModel

    var body: some View {
        TextField(
            UiMessages.string(.nativeSwiftTopicManagementFieldsDestinationTopicIdOrSlug, locale: nativeUiLocale),
            text: $viewModel.destinationIdOrSlug
        )
        .textFieldStyle(.roundedBorder)
    }
}
