import SwiftUI
import VouchaAPI
import VouchaDesignSystem

struct NativeSupportSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: NativeSupportViewModel

    init(client: APIClient?, routeMatch: NativeRouteMatch?) {
        _viewModel = State(initialValue: NativeSupportViewModel(client: client, routeMatch: routeMatch))
    }

    var body: some View {
        @Bindable
        var viewModel = viewModel

        HStack(alignment: .top, spacing: Spacing.md) {
            supportSidebar(viewModel: viewModel)
            Divider()
            supportDetail(viewModel: viewModel)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(Spacing.md)
        .task {
            await viewModel.load()
        }
    }

}
