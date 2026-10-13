import SwiftUI
import VouchaAPI

struct ListsRouteLoadedSurface: View {
    @State
    private var viewModel: ListsViewModel
    let isStaffViewer: Bool

    init(client: APIClient, isStaffViewer: Bool = false) {
        _viewModel = State(initialValue: ListsViewModel(client: client))
        self.isStaffViewer = isStaffViewer
    }

    var body: some View {
        ListsView(viewModel: viewModel, isStaffViewer: isStaffViewer)
    }
}
