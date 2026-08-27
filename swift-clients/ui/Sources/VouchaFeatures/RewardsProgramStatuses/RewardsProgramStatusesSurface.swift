import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct RewardsProgramStatusesRouteSurface: View {
    let client: APIClient?
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        if let client, isSignedIn {
            RewardsProgramStatusesSurface(
                viewModel: RewardsProgramStatusesViewModel(service: RewardsProgramStatusService(client: client))
            )
        } else {
            EmptyStateView(
                icon: "tag",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeAuthSignInToContinue),
                actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                action: showSignIn
            )
        }
    }
}

struct RewardsProgramStatusesSurface: View {
    @Environment(\.locale)
    private var locale
    @State
    var viewModel: RewardsProgramStatusesViewModel

    init(viewModel: RewardsProgramStatusesViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text(UiMessages.string(.nativeSwiftSettingsRewardStatuses, locale: locale)).font(.largeTitle)
                addStatusSection
                message(viewModel.searchErrorMessage)
                if viewModel.isLoading, viewModel.statuses.isEmpty {
                    ProgressView()
                } else {
                    message(viewModel.errorMessage)
                    if viewModel.errorMessage != nil {
                        Button(UiMessages.string(.nativeCommonRetry, locale: locale)) { Task { await viewModel.load() }
                        }
                    }
                    ForEach(viewModel.statuses) { RewardsProgramStatusRow(status: $0, viewModel: viewModel) }
                    message(viewModel.continuationErrorMessage)
                    if viewModel.errorMessage == nil {
                        HybridPaginationControl(
                            hasMore: viewModel.pagination.hasLoadedPage && viewModel.pagination.hasMore,
                            isLoading: viewModel.pagination.hasLoadedPage && viewModel.pagination.isLoading,
                            hasError: viewModel.continuationErrorMessage != nil,
                            isDisabled: viewModel.isLoading,
                            accessibilityIdentifier: "rewards-program-statuses-pagination"
                        ) { await viewModel.loadMore() }
                    }
                }
                message(viewModel.mutationErrorMessage)
            }.padding(16)
        }
        .task {
            if viewModel.statuses.isEmpty, !viewModel.isLoading {
                await viewModel.load()
            }
        }
    }

    private var addStatusSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(
                UiMessages.string(
                    .extractedRewardsProgramStatusesManagerAddStatusFormAddArewardsProgramStatus77e8d1de,
                    locale: locale
                )
            ).font(.headline)
            HStack {
                TextField(
                    UiMessages.string(
                        .extractedRewardsProgramStatusesManagerAddStatusFormSearchRewardsProgramStatusesA9286328,
                        locale: locale
                    ),
                    text: $viewModel.topicQuery
                )
                Button(UiMessages.string(.nativeSwiftCommonSearch, locale: locale)) {
                    Task { await viewModel.searchTopics() }
                }.disabled(viewModel.isSearching)
            }
            ForEach(viewModel.topicResults) { topic in
                if let name = topic.name {
                    HStack {
                        Text(verbatim: UiMessages.string(.userContent(name), locale: locale))
                        Spacer()
                        Button(
                            UiMessages.string(
                                .extractedRewardsProgramStatusesManagerAddStatusFormAdd9fd728c6,
                                locale: locale
                            )
                        ) {
                            Task { _ = await viewModel.create(rewardsProgramStatusId: topic.id) }
                        }.disabled(viewModel.isCreating)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func message(_ value: UiVerbatimText?) -> some View {
        if let value {
            Text(verbatim: UiMessages.string(value, locale: locale)).foregroundStyle(.red)
        }
    }
}
