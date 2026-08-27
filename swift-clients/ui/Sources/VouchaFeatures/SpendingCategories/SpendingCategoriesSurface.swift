import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct SpendingCategoriesRouteSurface: View {
    @Environment(\.locale) private var locale
    let client: APIClient?
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        if let client, isSignedIn {
            SpendingCategoriesSurface(
                viewModel: SpendingCategoriesViewModel(service: SpendingCategoryService(client: client)),
                locale: locale
            )
        } else {
            EmptyStateView(
                icon: "list.bullet.rectangle",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeAuthSignInToContinue),
                actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                action: showSignIn
            )
        }
    }
}

struct SpendingCategoriesSurface: View {
    @Environment(\.locale) private var locale
    @State var viewModel: SpendingCategoriesViewModel
    @State private var draft: SpendingCategoryDraft

    init(viewModel: SpendingCategoriesViewModel, locale: Locale = .current) {
        _viewModel = State(initialValue: viewModel)
        _draft = State(initialValue: SpendingCategoryDraft(locale: locale))
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text(UiMessages.string(.extractedSpendingCategoriesPageSpendingCategories3ed30dfb, locale: locale))
                    .font(.largeTitle)
                Text(UiMessages.string(
                    .extractedSpendingCategoriesPageManageYourSpendingCategoriesAndAmounts72fd1919,
                    locale: locale
                ))
                createSection
                message(viewModel.searchErrorMessage)
                if viewModel.isLoading, viewModel.categories.isEmpty {
                    ProgressView()
                } else {
                    message(viewModel.errorMessage)
                    if viewModel
                        .errorMessage !=
                        nil {
                        Button(UiMessages.string(.nativeCommonRetry, locale: locale)) { Task { await viewModel.load() }
                        }
                    }
                    ForEach(viewModel.categories) { SpendingCategoryRow(category: $0, viewModel: viewModel) }
                    message(viewModel.continuationErrorMessage)
                    if viewModel.errorMessage == nil {
                        HybridPaginationControl(
                            hasMore: viewModel.pagination.hasLoadedPage && viewModel.pagination.hasMore,
                            isLoading: viewModel.isLoadingMore,
                            hasError: viewModel.continuationErrorMessage != nil,
                            isDisabled: viewModel.isLoading,
                            accessibilityIdentifier: "spending-categories-pagination"
                        ) { await viewModel.loadMore() }
                    }
                }
                message(viewModel.mutationErrorMessage)
            }.padding(16)
        }.task {
            if viewModel.categories.isEmpty, !viewModel.isLoading {
                await viewModel.load()
            }
        }
        .onChange(of: locale.identifier) { _, _ in
            draft.applyLocale(locale)
        }
    }

    private var createSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(UiMessages.string(
                .extractedSpendingCategoriesManagerAddCategoryFormAddAspendingCategory0214ba9e,
                locale: locale
            )).font(.headline)
            TextField(
                UiMessages.string(.extractedSpendingCategoriesManagerAddCategoryFormAmount49e96d7c, locale: locale),
                text: Binding(
                    get: { draft.amountText },
                    set: { draft.updateAmountText($0) }
                )
            )
            Picker(
                UiMessages.string(.nativeSwiftCommonCurrency, locale: locale),
                selection: $draft.currency
            ) {
                ForEach(Currency.supported) { currency in
                    Text(currency.code.uppercased()).tag(currency.code)
                }
            }
            Picker(
                UiMessages.string(.extractedSpendingCategoriesManagerAddCategoryFormFrequency16b6668d, locale: locale),
                selection: $draft.frequency
            ) {
                Text(UiMessages.string(
                    .extractedSpendingCategoriesManagerFrequencySelectMonthly9b11f6b7,
                    locale: locale
                )).tag(SpendingFrequency.monthly)
                Text(UiMessages.string(
                    .extractedSpendingCategoriesManagerFrequencySelectAnnually1ec9d1d5,
                    locale: locale
                )).tag(SpendingFrequency.annually)
            }
            TextField(
                UiMessages
                    .string(.extractedSpendingCategoriesManagerAddCategoryFormOptionalNote951ddd37, locale: locale),
                text: $draft.note,
                axis: .vertical
            )
            HStack { TextField(
                UiMessages
                    .string(
                        .extractedSpendingCategoriesManagerAddCategoryFormSearchSpendingCategoriesA0c47f67,
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
                    HStack { Text(verbatim: UiMessages.string(
                        .userContent(name),
                        locale: locale
                    ))
                    Spacer()
                    Button(UiMessages.string(
                        .extractedSpendingCategoriesManagerAddCategoryFormAdd9fd728c6,
                        locale: locale
                    )) {
                        Task {
                            if await viewModel.create(
                                spendingCategoryId: topic.id,
                                draft: draft
                            ) {
                                draft = SpendingCategoryDraft(locale: locale)
                            }
                        }
                    }.disabled(viewModel.isCreating)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func message(_ value: UiVerbatimText?)
        -> some View {
        if let value {
            Text(verbatim: UiMessages.string(
                value,
                locale: locale
            )).foregroundStyle(.red)
        }
    }
}
