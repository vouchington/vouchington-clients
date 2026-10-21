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
    let onNavigate: (String) -> Void
    let currentUserId: String?
    let mcpSession: NativeChatMCPAgentSession?

    init(
        client: APIClient?, routeMatch: NativeRouteMatch?,
        currentUserId: String? = nil, mcpSession: NativeChatMCPAgentSession? = nil,
        onNavigate: @escaping (String) -> Void = { _ in }
    ) {
        _viewModel = State(initialValue: NativeChatViewModel(
            client: client, routeMatch: routeMatch, mcpSession: mcpSession,
            currentMemberId: currentUserId
        ))
        self.onNavigate = onNavigate
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
        .onChange(of: mcpSession?.revision) { _, _ in
            viewModel.invalidateMCPReceiptsIfNeeded()
        }
        .onChange(of: currentUserId) { _, memberId in
            viewModel.currentMemberId = memberId
            Task { await viewModel.loadConversations(reset: true) }
        }
    }

}
