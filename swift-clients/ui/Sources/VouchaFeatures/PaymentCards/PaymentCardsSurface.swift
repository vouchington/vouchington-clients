import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct PaymentCardsRouteSurface: View {
    let client: APIClient?
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        if let client, isSignedIn {
            PaymentCardsSurface(viewModel: PaymentCardsViewModel(service: PaymentCardService(client: client)))
        } else {
            EmptyStateView(
                icon: "creditcard",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeAuthSignInToContinue),
                actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                action: showSignIn
            )
        }
    }
}

struct PaymentCardsSurface: View {
    @Environment(\.locale)
    private var locale
    @State
    var viewModel: PaymentCardsViewModel

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text(UiMessages.string(.nativeSwiftSettingsCards, locale: locale)).font(.largeTitle)
                addCardSection
                if let error = viewModel.searchErrorMessage {
                    Text(verbatim: UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                }
                if viewModel.isLoading, viewModel.cards.isEmpty {
                    ProgressView()
                } else {
                    if let error = viewModel.errorMessage {
                        Text(verbatim: UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                        retryButton
                    }
                    ForEach(viewModel.cards) { PaymentCardRow(card: $0, viewModel: viewModel) }
                    if let error = viewModel.continuationErrorMessage {
                        Text(verbatim: UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                    }
                    if viewModel.errorMessage == nil {
                        HybridPaginationControl(
                            hasMore: viewModel.pagination.hasLoadedPage && viewModel.pagination.hasMore,
                            isLoading: viewModel.isLoadingMore,
                            hasError: viewModel.continuationErrorMessage != nil,
                            isDisabled: viewModel.isLoading,
                            accessibilityIdentifier: "payment-cards-pagination"
                        ) {
                            await viewModel.loadMore()
                        }
                    }
                }
                if let error = viewModel.mutationErrorMessage {
                    Text(verbatim: UiMessages.string(error, locale: locale)).foregroundStyle(.red)
                }
            }
            .padding(16)
        }
        .task {
            if viewModel.cards.isEmpty, !viewModel.isLoading {
                await viewModel.load()
            }
        }
    }

    private var addCardSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsAddCard, locale: locale)).font(.headline)
            HStack {
                TextField(
                    UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsSearch, locale: locale),
                    text: $viewModel.topicQuery
                )
                Button(UiMessages.string(.nativeSwiftCommonSearch, locale: locale)) {
                    Task { await viewModel.searchTopics() }
                }.disabled(viewModel.isSearching)
            }
            ForEach(viewModel.topicResults) { topic in
                HStack {
                    Text(verbatim: UiMessages.string(.userContent(topic.name ?? topic.slug ?? ""), locale: locale))
                    Spacer()
                    Button(UiMessages.string(.nativeSwiftHouseholdsBookmarksPaymentCardsAdd, locale: locale)) {
                        Task { await viewModel.create(topicId: topic.id) }
                    }.disabled(viewModel.isCreating)
                }
            }
        }
    }

    private var retryButton: some View {
        Button(UiMessages.string(.nativeCommonRetry, locale: locale)) { Task { await viewModel.load() } }
    }
}
