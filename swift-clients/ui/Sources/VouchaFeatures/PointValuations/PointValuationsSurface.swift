import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct PointValuationsRouteSurface: View {
    @Environment(\.locale)
    private var locale
    let client: APIClient?
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        if let client, isSignedIn {
            PointValuationsSurface(
                viewModel: PointValuationsViewModel(service: PointValuationService(client: client)),
                locale: locale
            )
        } else {
            EmptyStateView(
                icon: "chart.bar.doc.horizontal",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeAuthSignInToContinue),
                actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                action: showSignIn
            )
        }
    }
}

struct PointValuationsSurface: View {
    @Environment(\.locale)
    private var locale
    @State
    var viewModel: PointValuationsViewModel
    @State
    private var interactionState: PointValuationsSurfaceInteractionState

    init(
        viewModel: PointValuationsViewModel,
        locale: Locale = .current,
        interactionState: PointValuationsSurfaceInteractionState? = nil
    ) {
        _viewModel = State(initialValue: viewModel)
        _interactionState = State(
            initialValue: interactionState ?? PointValuationsSurfaceInteractionState(locale: locale)
        )
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                Text(
                    UiMessages.string(
                        .extractedRewardsProgramPointValuationsPagePointValuationsF90821b2,
                        locale: locale
                    )
                ).font(.largeTitle)
                Text(
                    UiMessages.string(
                        .extractedRewardsProgramPointValuationsPageManageYourRewardsProgramPointValuationsE3bcc429,
                        locale: locale
                    )
                )
                addValuationSection
                message(viewModel.searchErrorMessage)
                if viewModel.isLoading, viewModel.valuations.isEmpty {
                    ProgressView()
                } else {
                    message(viewModel.errorMessage)
                    if viewModel.errorMessage != nil {
                        retryButton
                    }
                    ForEach(viewModel.valuations) {
                        PointValuationRow(valuation: $0, viewModel: viewModel)
                    }
                    message(viewModel.continuationErrorMessage)
                    if viewModel.errorMessage == nil {
                        HybridPaginationControl(
                            hasMore: viewModel.pagination.hasLoadedPage && viewModel.pagination.hasMore,
                            isLoading: viewModel.isLoadingMore,
                            hasError: viewModel.continuationErrorMessage != nil,
                            isDisabled: viewModel.isLoading,
                            accessibilityIdentifier: "point-valuations-pagination"
                        ) { await viewModel.loadMore() }
                    }
                }
                message(viewModel.mutationErrorMessage)
            }
            .padding(16)
        }
        .task {
            if viewModel.valuations.isEmpty, !viewModel.isLoading {
                await viewModel.load()
            }
        }
        .onChange(of: locale, initial: true) { _, locale in
            interactionState.updateCreateDraftLocaleIfValueEmpty(locale)
        }
    }

    private var addValuationSection: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(
                UiMessages.string(
                    .extractedPointValuationsManagerAddValuationFormAddApointValuation29ced576,
                    locale: locale
                )
            ).font(.headline)
            TextField(
                UiMessages.string(
                    .extractedPointValuationsManagerAddValuationFormValuePerPointFa9d9f8a,
                    locale: locale
                ),
                text: $interactionState.createDraft.valuePerPointText
            )
            Picker(
                UiMessages.string(.nativeSwiftCommonCurrency, locale: locale),
                selection: $interactionState.createDraft.currency
            ) {
                ForEach(Currency.supported) { currency in
                    Text(currency.code.uppercased()).tag(currency.code)
                }
            }
            TextField(
                UiMessages.string(
                    .extractedPointValuationsManagerAddValuationFormOptionalNote951ddd37,
                    locale: locale
                ),
                text: $interactionState.createDraft.note,
                axis: .vertical
            )
            HStack {
                TextField(
                    UiMessages.string(
                        .extractedPointValuationsManagerAddValuationFormSearchRewardsPrograms990e4fa0,
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
                                .extractedPointValuationsManagerAddValuationFormAdd9fd728c6,
                                locale: locale
                            )
                        ) {
                            Task {
                                if await viewModel.create(
                                    rewardsProgramId: topic.id,
                                    draft: interactionState.createDraft
                                ) {
                                    interactionState.createDraft = PointValuationDraft(locale: locale)
                                }
                            }
                        }.disabled(
                            viewModel.isCreating || !viewModel.canCreate(rewardsProgramId: topic.id)
                        )
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

    private var retryButton: some View {
        Button(UiMessages.string(.nativeCommonRetry, locale: locale)) { Task { await viewModel.load() } }
    }
}
