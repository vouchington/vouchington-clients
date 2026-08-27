import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeTagManagementSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: NativeTagManagementViewModel
    let isSignedIn: Bool
    let canCreateVote: Bool
    let showSignIn: () -> Void
    let onNavigate: (String) -> Void

    init(
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        subjectKind: NativeTagManagementSubjectKind,
        subjectId: String? = nil,
        subjectTitle: String? = nil,
        isSignedIn: Bool,
        canCreateVote: Bool = true,
        showSignIn: @escaping () -> Void,
        onNavigate: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(
            initialValue: NativeTagManagementViewModel(
                client: client,
                routeMatch: routeMatch,
                subjectKind: subjectKind,
                subjectId: subjectId,
                subjectTitle: subjectTitle
            )
        )
        self.isSignedIn = isSignedIn
        self.canCreateVote = canCreateVote
        self.showSignIn = showSignIn
        self.onNavigate = onNavigate
    }

    var body: some View {
        @Bindable
        var boundViewModel = viewModel

        VStack(alignment: .leading, spacing: Spacing.md) {
            header(viewModel: boundViewModel)

            if !isSignedIn {
                VStack(alignment: .leading, spacing: Spacing.sm) {
                    EmptyStateView(
                        icon: "lock",
                        title: .message(.nativeSwiftEmptyStateSignInRequired),
                        message: .message(.nativeSwiftEmptyStateSignInTagsMessage)
                    )
                    Button(UiMessages.string(.nativeSwiftTagManagementSignIn, locale: nativeUiLocale)) {
                        showSignIn()
                    }
                    .buttonStyle(.borderedProminent)
                }
            } else if case let .error(error) = boundViewModel.state {
                ErrorStateView(error: error) {
                    await viewModel.load()
                }
            } else {
                tagTabs(viewModel: boundViewModel)
                addTagForm(viewModel: boundViewModel)
                currentTags(viewModel: boundViewModel)
            }
        }
        .task {
            await viewModel.load()
        }
        .task(id: boundViewModel.searchQuery) {
            await viewModel.search()
        }
        .onChange(of: boundViewModel.activeTab) { _, _ in
            Task {
                viewModel.clearSearch()
                await viewModel.reloadRelations()
            }
        }
        .onChange(of: boundViewModel.publisherTypeSelection) { _, newValue in
            guard !newValue.isEmpty else { return }
            Task {
                await viewModel.addSelectedPublisherType(id: newValue)
            }
        }
    }
}
