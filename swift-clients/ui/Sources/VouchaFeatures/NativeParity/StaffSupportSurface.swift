import SwiftUI
import VouchaAPI
import VouchaLocalization
import VouchaModels

struct StaffSupportSurface: View {
    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.locale) var nativeUiLocale
    @State private var viewModel: StaffSupportViewModel
    @State private var columnState = StaffSupportColumnState()
    @State var pendingConfirmation: StaffSupportConfirmation?
    private let onNavigateToTargetPath: (String) -> Void

    init(
        client: APIClient?,
        administratorId: String?,
        mode: StaffSupportMode,
        routeMatch: NativeRouteMatch?,
        onNavigateToTargetPath: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: StaffSupportViewModel(
            client: client,
            administratorId: administratorId,
            mode: mode,
            routeMatch: routeMatch
        ))
        self.onNavigateToTargetPath = onNavigateToTargetPath
    }

    var body: some View {
        @Bindable var viewModel = viewModel
        @Bindable var columnState = columnState
        NavigationSplitView(columnVisibility: $columnState.visibility) {
            staffSupportSidebar(viewModel) {
                columnState.update(for: horizontalSizeClass, hasDetailSelection: true)
            }
            .navigationSplitViewColumnWidth(min: 280, ideal: 320)
        } detail: {
            staffSupportDetail(viewModel, onNavigateToTargetPath: onNavigateToTargetPath)
        }
        .navigationSplitViewStyle(.balanced)
        .task {
            await viewModel.load()
            columnState.update(for: horizontalSizeClass, hasDetailSelection: hasDetailSelection(viewModel))
        }
        .onChange(of: horizontalSizeClass) { _, sizeClass in
            columnState.update(for: sizeClass, hasDetailSelection: hasDetailSelection(viewModel))
        }
        .onDisappear { viewModel.cancelDraftPolling() }
        .confirmationDialog(
            UiMessages.string(
                pendingConfirmation?.titleKey ?? .nativeSwiftSupportResolveThreadConfirmationTitle,
                locale: nativeUiLocale
            ),
            isPresented: Binding(
                get: { pendingConfirmation != nil },
                set: {
                    if !$0 {
                        pendingConfirmation = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            if let confirmation = pendingConfirmation {
                Button(UiMessages.string(confirmation.buttonKey, locale: nativeUiLocale)) {
                    run(confirmation, viewModel: viewModel)
                }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                pendingConfirmation = nil
            }
        }
    }

    private func hasDetailSelection(_ viewModel: StaffSupportViewModel) -> Bool {
        viewModel.selectedThread != nil || viewModel.selectedContact != nil
    }

    private func run(_ confirmation: StaffSupportConfirmation, viewModel: StaffSupportViewModel) {
        pendingConfirmation = nil
        Task {
            switch confirmation {
            case let .approve(message): await viewModel.approve(message)
            case let .send(message): await viewModel.send(message)
            case let .setResolved(resolved): await viewModel.setResolved(resolved)
            }
        }
    }
}

enum StaffSupportConfirmation {
    case approve(SupportMessage)
    case send(SupportMessage)
    case setResolved(Bool)

    var titleKey: UiMessageKey {
        switch self {
        case .approve: .nativeSwiftSupportApproveDraftConfirmationTitle
        case .send: .nativeSwiftSupportSendDraftConfirmationTitle
        case let .setResolved(resolved):
            resolved
                ? .nativeSwiftSupportResolveThreadConfirmationTitle
                : .nativeSwiftSupportReopenThreadConfirmationTitle
        }
    }

    var buttonKey: UiMessageKey {
        switch self {
        case .approve: .nativeSwiftCommonApprove
        case .send: .nativeSwiftCommonSend
        case let .setResolved(resolved):
            resolved ? .nativeSwiftCommunityActionsResolve : .nativeSwiftCommunityActionsReopen
        }
    }
}
