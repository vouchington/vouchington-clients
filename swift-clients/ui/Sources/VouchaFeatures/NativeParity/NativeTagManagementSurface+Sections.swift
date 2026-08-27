import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension NativeTagManagementSurface {
    func header(viewModel: NativeTagManagementViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(UiMessages.string(viewModel.managementTitle, locale: nativeUiLocale))
                .font(Typography.headline)
            Text(UiMessages.string(
                .joined([viewModel.subjectTitle, viewModel.subjectDetail].compactMap { $0 }),
                locale: nativeUiLocale
            ))
            .font(Typography.subheadline)
            .foregroundStyle(Colors.secondaryLabel)
            .fixedSize(horizontal: false, vertical: true)
        }
    }

    func tagTabs(viewModel: NativeTagManagementViewModel) -> some View {
        Picker(
            UiMessages.string(.nativeSwiftTagManagementTagGroup, locale: nativeUiLocale),
            selection: Binding(get: { viewModel.activeTab }, set: { viewModel.activeTab = $0 })
        ) {
            ForEach(viewModel.tabs) { tab in
                Text(UiMessages.string(tab.label, locale: nativeUiLocale)).tag(tab.value)
            }
        }
        .pickerStyle(.segmented)
    }

    @ViewBuilder
    func addTagForm(viewModel: NativeTagManagementViewModel) -> some View {
        if viewModel.tagLimitReached {
            NativeTagLimitCta(onNavigate: onNavigate)
        } else if let config = viewModel.activeTabConfig {
            if config.predicate == "publisher_type" || viewModel.subjectKind == .user {
                NativeTagPublisherPicker(viewModel: viewModel)
            } else {
                NativeTagSearchForm(viewModel: viewModel, config: config)
            }
        }
    }

    func currentTags(viewModel: NativeTagManagementViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftTagManagementCurrentTags, locale: nativeUiLocale))
                .font(Typography.subheadline.bold())

            if viewModel.relations.isEmpty {
                EmptyStateView(
                    icon: "tag",
                    title: .message(.nativeSwiftEmptyStateNoTags),
                    message: .message(.nativeSwiftEmptyStateAddTagsMessage)
                )
            } else {
                NativeTagRelationList(viewModel: viewModel, canCreateVote: canCreateVote)
            }
            HybridPaginationControl(
                hasMore: viewModel.relationPagination.hasLoadedPage && viewModel.relationPagination.hasMore,
                isLoading: viewModel.relationPagination.isLoading,
                hasError: viewModel.relationPagination.lastError != nil,
                accessibilityIdentifier: "tag-relations-pagination"
            ) {
                await viewModel.loadMoreRelations()
            }
        }
    }
}
