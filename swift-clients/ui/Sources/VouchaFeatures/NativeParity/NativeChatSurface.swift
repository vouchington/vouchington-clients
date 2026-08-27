import SwiftUI
import VouchaAPI
import VouchaDesignSystem

struct NativeChatSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: NativeChatViewModel

    init(client: APIClient?, routeMatch: NativeRouteMatch?) {
        _viewModel = State(initialValue: NativeChatViewModel(client: client, routeMatch: routeMatch))
    }

    var body: some View {
        @Bindable
        var viewModel = viewModel

        HStack(alignment: .top, spacing: Spacing.md) {
            chatSidebar(viewModel: viewModel)

            Divider()

            chatDetail(viewModel: viewModel)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .padding(Spacing.md)
        .task {
            await viewModel.load()
        }
    }

}
