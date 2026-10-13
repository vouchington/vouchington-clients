import SwiftUI
import VouchaAPI
import VouchaDesignSystem

struct NativeChatSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    @State
    private var viewModel: NativeChatViewModel
    @State
    var mcpPresentationWindow = NativeChatMCPWindowReference()
    let currentUserId: String?
    let mcpSession: NativeChatMCPAgentSession?

    init(
        client: APIClient?, routeMatch: NativeRouteMatch?,
        currentUserId: String? = nil, mcpSession: NativeChatMCPAgentSession? = nil
    ) {
        _viewModel = State(initialValue: NativeChatViewModel(
            client: client, routeMatch: routeMatch, mcpSession: mcpSession
        ))
        self.currentUserId = currentUserId
        self.mcpSession = mcpSession
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
        .background(NativeChatMCPWindowReader { mcpPresentationWindow.value = $0 })
        .task {
            await viewModel.load()
        }
    }

}
