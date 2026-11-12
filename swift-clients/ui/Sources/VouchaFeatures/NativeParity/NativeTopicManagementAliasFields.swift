import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

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
            ForEach(viewModel.aliases) { alias in
                HStack {
                    Text(verbatim: alias.alias)
                    Spacer(minLength: 0)
                    if canRemove(alias) {
                        Button {
                            Task { await viewModel.removeAlias(alias) }
                        } label: {
                            Image(systemName: "trash")
                        }
                        .buttonStyle(.plain)
                    }
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

    func canRemove(_ alias: TopicAlias) -> Bool {
        alias.alias != viewModel.topic?.slug
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
        if let message = viewModel.mergeMessage {
            Text(UiMessages.string(message, locale: nativeUiLocale))
                .accessibilityIdentifier("topic-merge-status")
        }
        if let operation = viewModel.mergeOperation {
            Text(verbatim: UiMessages.string(.protocolValue(operation.status), locale: nativeUiLocale))
                .accessibilityIdentifier("topic-merge-producer-status")
            Text(verbatim: UiMessages.string(.protocolValue(operation.stage), locale: nativeUiLocale))
                .accessibilityIdentifier("topic-merge-stage")
            if let completedAt = operation.completedAt {
                Text(completedAt, style: .date)
                    .accessibilityIdentifier("topic-merge-completed-at")
            }
            if let errorCode = operation.errorCode {
                Text(verbatim: UiMessages.string(.protocolValue(errorCode), locale: nativeUiLocale))
                    .accessibilityIdentifier("topic-merge-error-code")
            }
        }
        if viewModel.canCheckMergeStatus {
            Button(UiMessages.string(.settingsTopicsMergeCheckStatus, locale: nativeUiLocale)) {
                Task { await viewModel.checkMergeStatus() }
            }
            .disabled(viewModel.isCheckingMerge)
        }
    }
}
