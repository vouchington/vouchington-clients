import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

public struct ListsView: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Bindable
    public var viewModel: ListsViewModel
    @State
    private var showingCreate = false
    @State
    private var showingEdit = false
    @State
    private var showingImport = false
    @State
    private var confirmingDelete = false

    public init(viewModel: ListsViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                controls
                content
            }
            .padding(Spacing.md)
        }
        .navigationTitle(UiMessages.string(.nativeSwiftListsLists, locale: nativeUiLocale))
        .task { await viewModel.load() }
        .refreshable { await viewModel.reload() }
        .sheet(isPresented: $showingCreate) {
            ListsCreateSheet { await viewModel.createList(name: $0) }
        }
        .sheet(isPresented: $showingEdit) {
            if let list = viewModel.selectedList {
                ListsEditSheet(list: list) { name, description in
                    await viewModel.updateSelectedList(name: name, description: description)
                }
            }
        }
        .sheet(isPresented: $showingImport) {
            ListsImportSheet { await viewModel.importCommunity(slug: $0) }
        }
        .confirmationDialog(
            UiMessages.string(.nativeSwiftListsDeleteListConfirmationTitle, locale: nativeUiLocale),
            isPresented: $confirmingDelete,
            titleVisibility: .visible
        ) {
            Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                Task { await viewModel.deleteSelectedList() }
            }
        }
    }

    private var controls: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack(spacing: Spacing.sm) {
                Button(UiMessages.string(.nativeSwiftCommonCreate, locale: nativeUiLocale)) { showingCreate = true }
                    .buttonStyle(.borderedProminent)
                Button(UiMessages.string(.nativeSwiftCommonEdit, locale: nativeUiLocale)) { showingEdit = true }
                    .disabled(viewModel.selectedList == nil)
                Button(UiMessages.string(.nativeSwiftCommonImport, locale: nativeUiLocale)) { showingImport = true }
                    .disabled(viewModel.selectedList == nil)
                Button(UiMessages.string(.nativeSwiftCommonDelete, locale: nativeUiLocale), role: .destructive) {
                    confirmingDelete = true
                }
                .disabled(viewModel.selectedList == nil)
            }
            Picker(
                UiMessages.string(.nativeSwiftListsFilter, locale: nativeUiLocale),
                selection: $viewModel.selectedFilter
            ) {
                ForEach(ListsItemFilter.allCases) { filter in
                    Text(UiMessages.string(filter.titleKey, locale: nativeUiLocale)).tag(filter)
                }
            }
            .pickerStyle(.segmented)
            .onChange(of: viewModel.selectedFilter) { _, filter in
                Task { await viewModel.selectFilter(filter) }
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch viewModel.state {
        case .loading where viewModel.lists.isEmpty:
            LoadingView()
        case let .error(error) where viewModel.lists.isEmpty:
            ErrorStateView(error: error) { await viewModel.reload() }
        default:
            if viewModel.lists.isEmpty {
                EmptyStateView(
                    icon: "list.bullet.rectangle",
                    title: .message(.nativeSwiftEmptyStateNoLists),
                    message: .message(.nativeSwiftEmptyStateNoListsMessage)
                )
            } else {
                VStack(alignment: .leading, spacing: Spacing.md) {
                    mutationError
                    listPicker
                    selectedListItems
                }
            }
        }
    }

    @ViewBuilder
    private var mutationError: some View {
        if case let .error(error) = viewModel.state {
            ErrorStateView(error: error) { await viewModel.reload() }
                .frame(minHeight: 180)
        }
    }

}

struct ListsRouteSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let client: APIClient?
    let isSignedIn: Bool
    let showSignIn: () -> Void

    var body: some View {
        if !isSignedIn || client == nil {
            EmptyStateView(
                icon: "person.crop.circle.badge.exclamationmark",
                title: .message(.nativeSwiftEmptyStateSignInRequired),
                message: .message(.nativeSwiftEmptyStateSignInListsMessage),
                actionTitle: .message(.nativeSwiftEmptyStateSignIn),
                action: showSignIn
            )
        } else if let client {
            ListsRouteLoadedSurface(client: client)
        }
    }
}
