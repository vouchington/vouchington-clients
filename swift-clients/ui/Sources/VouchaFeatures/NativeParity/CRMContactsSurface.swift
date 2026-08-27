import SwiftUI
import VouchaAPI

struct CRMContactsSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @Environment(\.timeZone)
    var nativeUiTimeZone
    @State
    private var viewModel: CRMContactsViewModel

    init(client: APIClient?, routeMatch: NativeRouteMatch?) {
        self.init(viewModel: CRMContactsViewModel(client: client, routeMatch: routeMatch))
    }

    init(viewModel: CRMContactsViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        @Bindable
        var viewModel = viewModel

        HStack(alignment: .top, spacing: 0) {
            sidebarList(viewModel: viewModel)
                .frame(width: 340)
            Divider()
            detailList(viewModel: viewModel)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
        .task {
            await viewModel.load()
        }
        .onChange(of: viewModel.selectedStatus) { _, _ in
            Task { await viewModel.reloadContacts() }
        }
        .onChange(of: viewModel.selectedVertical) { _, _ in
            Task { await viewModel.reloadContacts() }
        }
        .onChange(of: viewModel.selectedLinkedFilter) { _, _ in
            Task { await viewModel.reloadContacts() }
        }
    }
}
