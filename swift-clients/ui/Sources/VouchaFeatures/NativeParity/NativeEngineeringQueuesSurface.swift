import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeEngineeringQueuesSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let entry: NativeRouteCatalogEntry
    @State
    private var viewModel: NativeEngineeringQueuesViewModel?

    init(entry: NativeRouteCatalogEntry, client: APIClient?) {
        self.entry = entry
        _viewModel = State(initialValue: client.map(NativeEngineeringQueuesViewModel.init(client:)))
    }

    var body: some View {
        if let viewModel {
            content(viewModel)
        } else {
            EmptyStateView(
                icon: "lock",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftEmptyStateSignInEngineeringQueuesMessage)
            )
        }
    }

    func content(_ viewModel: NativeEngineeringQueuesViewModel) -> some View {
        let errorBinding = Binding(
            get: { viewModel.actionErrorMessage != nil },
            set: {
                if !$0 {
                    viewModel.actionErrorMessage = nil
                }
            }
        )

        return ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                NativeRouteDestinationHeader(entry: entry)
                switch viewModel.state {
                case .loading where viewModel.queues.isEmpty:
                    ProgressView()
                        .frame(maxWidth: .infinity, alignment: .center)
                        .padding(.vertical, Spacing.xl)
                case let .error(error) where viewModel.queues.isEmpty:
                    ErrorStateView(error: error) {
                        await viewModel.load()
                    }
                default:
                    loadedContent(viewModel)
                }
            }
            .padding(Spacing.md)
        }
        .task {
            await viewModel.load()
        }
        .alert(
            UiMessages.string(.nativeSwiftEngineeringPostgresqlOperationFailed, locale: nativeUiLocale),
            isPresented: errorBinding
        ) {
            Button(UiMessages.string(.nativeSwiftCommonOK, locale: nativeUiLocale), role: .cancel) {}
        } message: {
            Text(UiMessages.string(
                viewModel.actionErrorMessage ?? .verbatim(""),
                locale: nativeUiLocale
            ))
        }
    }
}
