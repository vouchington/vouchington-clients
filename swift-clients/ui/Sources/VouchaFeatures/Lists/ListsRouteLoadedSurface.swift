import SwiftUI
import VouchaAPI

struct ListsRouteLoadedSurface: View {
    @State
    private var viewModel: ListsViewModel

    init(client: APIClient) {
        _viewModel = State(initialValue: ListsViewModel(client: client))
    }

    var body: some View {
        ListsView(viewModel: viewModel)
    }
}
