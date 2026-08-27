import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeEngineeringValkeySurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let entry: NativeRouteCatalogEntry
    @State
    private var viewModel: NativeEngineeringValkeyViewModel?
    @State
    private var pendingRebuildFilter: EngineeringValkeyBloomFilterTarget?
    @State
    var pendingFlushConcern: EngineeringValkeyFlushConcern?

    init(entry: NativeRouteCatalogEntry, client: APIClient?) {
        self.entry = entry
        _viewModel = State(initialValue: client.map(NativeEngineeringValkeyViewModel.init(client:)))
    }

    var body: some View {
        if let viewModel {
            content(viewModel)
        } else {
            EmptyStateView(
                icon: "lock",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftEmptyStateSignInEngineeringValkeyMessage)
            )
        }
    }

    func content(_ viewModel: NativeEngineeringValkeyViewModel) -> some View {
        sections(viewModel)
            .modifier(ValkeyActionErrorModifier(viewModel: viewModel))
            .modifier(
                ValkeyRebuildConfirmationModifier(
                    pendingFilter: $pendingRebuildFilter,
                    viewModel: viewModel
                )
            )
            .modifier(
                ValkeyFlushConfirmationModifier(
                    pendingConcern: $pendingFlushConcern,
                    viewModel: viewModel
                )
            )
            .confirmationDialog(
                UiMessages.string(.nativeSwiftEngineeringValkeyClearCachesConfirmationTitle, locale: nativeUiLocale),
                isPresented: Binding(
                    get: { viewModel.pendingAction != nil },
                    set: {
                        if !$0 {
                            viewModel.pendingAction = nil
                        }
                    }
                ),
                titleVisibility: .visible
            ) {
                Button(UiMessages.string(.nativeSwiftCommonConfirm, locale: nativeUiLocale), role: .destructive) {
                    Task { await viewModel.confirmPendingAction() }
                }
                Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {}
            } message: {
                if let message = confirmMessage(for: viewModel.pendingAction) {
                    Text(UiMessages.string(message, locale: nativeUiLocale))
                }
            }
            .task {
                await viewModel.load()
            }
    }

    private func sections(_ viewModel: NativeEngineeringValkeyViewModel) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                NativeRouteDestinationHeader(entry: entry)
                switch viewModel.state {
                case .loading where viewModel.cacheGroups.isEmpty:
                    ProgressView()
                        .frame(maxWidth: .infinity, alignment: .center)
                        .padding(.vertical, Spacing.xl)
                case let .error(error) where viewModel.cacheGroups.isEmpty:
                    ErrorStateView(error: error) {
                        await viewModel.load()
                    }
                default:
                    rebuildSection(viewModel)
                    cacheSection(viewModel)
                    flushSection(viewModel)
                }
            }
            .padding(Spacing.md)
        }
    }

    func rebuildSection(_ viewModel: NativeEngineeringValkeyViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            sectionTitle(UiMessages.string(.nativeSwiftEngineeringValkeyBloomFilterRebuild, locale: nativeUiLocale))
            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(EngineeringValkeyBloomFilterTarget.webVisibleCases, id: \.self) { filter in
                    VStack(alignment: .leading, spacing: Spacing.sm) {
                        NativeSurfaceRow(
                            row: .init(
                                icon: "arrow.triangle.2.circlepath",
                                title: filter.uiMessage,
                                detail: UiMessage(
                                    .nativeSwiftEngineeringValkeyFilterIdentifier,
                                    parameters: ["value": filter.rawValue]
                                )
                            )
                        )
                        HStack {
                            Spacer()
                            Button {
                                pendingRebuildFilter = filter
                            } label: {
                                Label(
                                    UiMessages.string(.nativeSwiftEngineeringValkeyRebuild, locale: nativeUiLocale),
                                    systemImage: "hammer"
                                )
                            }
                            .buttonStyle(.bordered)
                            .disabled(viewModel.isActionLoading("rebuild|\(filter.rawValue)"))
                        }
                    }
                }
            }
        }
    }

}
