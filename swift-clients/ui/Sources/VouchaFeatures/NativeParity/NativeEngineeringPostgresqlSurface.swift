import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeEngineeringPostgresqlSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let entry: NativeRouteCatalogEntry
    @State
    private var viewModel: NativeEngineeringPostgresqlViewModel?

    init(entry: NativeRouteCatalogEntry, client: APIClient?) {
        self.entry = entry
        _viewModel = State(initialValue: client.map(NativeEngineeringPostgresqlViewModel.init(client:)))
    }

    var body: some View {
        if let viewModel {
            content(viewModel)
        } else {
            EmptyStateView(
                icon: "lock",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftEmptyStateSignInEngineeringPostgresqlMessage)
            )
        }
    }

    // swiftlint:disable:next function_body_length
    func content(_ viewModel: NativeEngineeringPostgresqlViewModel) -> some View {
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
                case .loading where viewModel.migrations == nil:
                    ProgressView()
                        .frame(maxWidth: .infinity, alignment: .center)
                        .padding(.vertical, Spacing.xl)
                case let .error(error) where viewModel.migrations == nil:
                    ErrorStateView(error: error) {
                        await viewModel.load()
                    }
                default:
                    migrationSection(viewModel)
                    partitionSection(viewModel)
                    jobSection(viewModel)
                    articleSyncSection(viewModel)
                }
            }
            .padding(Spacing.md)
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
        .confirmationDialog(
            UiMessages.string(.nativeSwiftEngineeringPostgresqlConfirmPostgreSqloperation, locale: nativeUiLocale),
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
            Button(
                UiMessages.string(confirmButtonTitle(for: viewModel.pendingAction), locale: nativeUiLocale),
                role: .destructive
            ) {
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
}
