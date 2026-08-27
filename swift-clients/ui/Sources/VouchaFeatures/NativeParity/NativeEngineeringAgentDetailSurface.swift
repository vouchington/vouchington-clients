import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

struct NativeEngineeringAgentDetailSurface: View {
    @Environment(\.locale)
    private var locale
    @Bindable var viewModel: NativeRouteSurfaceViewModel
    let onNavigate: (String) -> Void
    @State private var filterKind: AgentConversationFilterKind = .username
    @State private var filterValue = ""

    init(viewModel: NativeRouteSurfaceViewModel, onNavigate: @escaping (String) -> Void) {
        self.viewModel = viewModel
        self.onNavigate = onNavigate
        _filterKind = State(initialValue: viewModel.agentConversationFilter?.kind ?? .username)
        _filterValue = State(initialValue: viewModel.agentConversationFilter?.value ?? "")
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if let detail = viewModel.agentDetail {
                metadata(detail)
            }
            filterControls
            rows(viewModel.agentConversationListRows())
            continuation
        }
    }

    private var filterControls: some View {
        HStack(spacing: Spacing.sm) {
            Picker("", selection: $filterKind) {
                ForEach(AgentConversationFilterKind.allCases, id: \.self) { kind in
                    Text(UiMessages.string(Self.filterLabel(kind), locale: locale)).tag(kind)
                }
            }
            .accessibilityLabel(UiMessages.string(.nativeSwiftRouteSurfaceAgentFilterKind, locale: locale))
            TextField("", text: $filterValue)
                .accessibilityLabel(Self.filterValueAccessibilityLabel(filterKind, locale: locale))
            Button(UiMessages.string(.nativeSwiftCommonSearch, locale: locale)) {
                Task { await viewModel.reloadAgentConversations(filter: selectedFilter) }
            }
            Button(UiMessages.string(.nativeSwiftCommonClear, locale: locale)) {
                filterValue = ""
                Task { await viewModel.reloadAgentConversations(filter: nil) }
            }
        }
    }

    private var selectedFilter: AgentConversationFilter? {
        AgentConversationFilter(kind: filterKind, value: filterValue)
    }

    @ViewBuilder
    private func rows(_ rows: [NativeRouteDestinationRow]) -> some View {
        if viewModel.isLoadingAgentConversations, rows.isEmpty {
            ProgressView().frame(maxWidth: .infinity, alignment: .center)
        } else if rows.isEmpty {
            EmptyStateView(
                icon: "square.stack.3d.down.right",
                title: .message(.nativeSwiftRouteSurfaceNoResults)
            )
        } else {
            NativeRowsSurface(rows: rows, state: .loaded, retry: nil, onNavigate: onNavigate)
        }
    }

    private var continuation: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            if viewModel.canLoadMoreAgentConversations || viewModel.agentConversationsPageErrorMessage != nil {
                Button(UiMessages.string(
                    viewModel
                        .agentConversationsPageErrorMessage == nil ? .nativeSwiftCommonLoadMore : .nativeCommonRetry,
                    locale: locale
                )) {
                    Task { await viewModel.loadMoreAgentConversations() }
                }
                .disabled(viewModel.isLoadingMoreAgentConversations)
            }
            if let error = viewModel.agentConversationsPageErrorMessage {
                Text(UiMessages.string(error, locale: locale)).foregroundStyle(.red)
            }
        }
    }

    static func filterValueAccessibilityLabel(_ kind: AgentConversationFilterKind, locale: Locale) -> String {
        UiMessages.string(
            .nativeSwiftRouteSurfaceAgentFilterValue,
            parameters: ["filter": UiMessages.string(filterLabel(kind), locale: locale)],
            locale: locale
        )
    }

    private static func filterLabel(_ kind: AgentConversationFilterKind) -> UiMessageKey {
        switch kind {
        case .userId: .nativeSwiftRouteSurfaceAgentFilterUserId
        case .username: .nativeSwiftRouteSurfaceAgentFilterUsername
        case .postId: .nativeSwiftRouteSurfaceAgentFilterPostId
        case .postSlug: .nativeSwiftRouteSurfaceAgentFilterPostSlug
        case .rssFeedItemId: .nativeSwiftRouteSurfaceAgentFilterRssFeedItemId
        }
    }

    private func metadata(_ detail: AgentDetailResponse) -> some View {
        let active = detail.agent.activatedAt != nil && detail.agent.deactivatedAt == nil
        return VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(verbatim: viewModel.agentDisplayName(detail)).font(Typography.headline)
            labeled(.nativeSwiftRouteSurfaceAgentType, value: .protocolValue(detail.agent.agentType))
            labeled(active ? .nativeSwiftRouteSurfaceAgentStatusActive : .nativeSwiftRouteSurfaceAgentStatusInactive)
            labeled(.nativeSwiftRouteSurfaceAgentId, value: .protocolValue(detail.agent.id))
            labeled(.nativeSwiftRouteSurfaceAgentSystemUserId, value: .protocolValue(detail.agent.systemUserId))
            labeled(
                .nativeSwiftRouteSurfaceCreated,
                value: .verbatim(UiMessages.date(detail.agent.createdAt, locale: locale, timeZone: .current))
            )
        }
    }

    private func labeled(_ key: UiMessageKey, value: UiVerbatimText? = nil) -> some View {
        Text(UiMessages.string(
            value.map { .joined([.message(key), $0], separator: ": ") } ?? .message(key),
            locale: locale
        ))
    }
}
