import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class NativeChatViewModel {
    let client: APIClient?
    @ObservationIgnored
    let titleProviderResolver: any NativeChatTitleProviderResolving
    private let initialConversationId: String?
    let logger = VouchaLogger(category: "NativeChatViewModel")
    @ObservationIgnored
    var loadedConversationDetailId: String?
    @ObservationIgnored
    var activeConversationDetailLoadRevision = 0
    @ObservationIgnored
    var activeConversationListLoadRevision = 0
    @ObservationIgnored
    var conversationListPageRequestRevision = 0
    var streamTask: Task<Void, Never>?
    var streamingConversationId: String?
    var streamingUserMessageId: String?
    var streamingAssistantMessageId: String?
    var pendingLocalAssistantMessageId: String?
    var createdConversationIds: Set<String> = []
    var selectionRevision = 0
    var olderMessagesLoadRevision = 0
    @ObservationIgnored
    let titleProviderPersistenceState = NativeChatTitleProviderPersistenceState()
    var titleProviderPersistenceTask: Task<Void, Never>?

    var conversationPagination = CursorPaginationState<ChatConversation>()
    var conversations: [ChatConversation] {
        get { conversationPagination.items }
        set { conversationPagination.replaceItems(newValue) }
    }

    var detailPageInfo: NativePaginationState?
    var selectedConversationId: String? {
        didSet {
            if pendingLocalTurn?.context.conversationId != selectedConversationId {
                pendingLocalTurn = nil
            }
        }
    }

    var messages: [NativeChatTimelineMessage] = []
    var draftMessage = "" {
        didSet {
            if !isSendingDraft, let pendingLocalTurn,
               draftMessage.trimmingCharacters(in: .whitespacesAndNewlines) != pendingLocalTurn.context.text {
                self.pendingLocalTurn = nil
            }
        }
    }

    @ObservationIgnored
    var pendingLocalTurn: NativeChatPendingLocalTurn?
    var listErrorMessage: UiVerbatimText?
    var detailErrorMessage: UiVerbatimText?
    var streamErrorMessage: UiVerbatimText?
    var isLoadingList = false
    var isLoadingDetail = false
    var isLoadingOlderMessages = false
    var isSendingDraft = false
    var isStreaming = false
    var isGeneratingTitle = false
    var streamedContent = ""
    var conversationTitleDraft = ""
    var titleProviderSelection: NativeChatTitleProviderKind {
        didSet {
            if pendingLocalTurn?.context.providerSelection != titleProviderSelection {
                pendingLocalTurn = nil
            }
        }
    }

    init(
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        titleProviderResolver: any NativeChatTitleProviderResolving = NativeChatLiveTitleProviderResolver()
    ) {
        self.client = client
        self.titleProviderResolver = titleProviderResolver
        initialConversationId = routeMatch?.param("id")
        selectedConversationId = initialConversationId
        titleProviderSelection = titleProviderResolver.defaultSelection()
    }

    var selectedConversation: ChatConversation? {
        guard let selectedConversationId else { return nil }
        return conversations.first { $0.id == selectedConversationId }
    }

    var selectedConversationTitle: UiVerbatimText {
        guard let title = selectedConversation?.title.trimmingCharacters(in: .whitespacesAndNewlines),
              title.isEmpty == false
        else {
            return .message(.nativeSwiftChatNewConversation)
        }
        return .verbatim(title)
    }

    var canLoadMoreConversations: Bool {
        conversationPagination.hasMore && !conversationPagination.isLoading
    }

    var canLoadOlderMessages: Bool {
        detailPageInfo?.hasNextPage == true && !isLoadingOlderMessages
    }

    var isLoadingMoreConversations: Bool {
        conversationPagination.isLoading && conversationPagination.hasLoadedPage
    }

    func load() async {
        await loadConversations(reset: true)
        if let initialConversationId {
            await selectConversation(id: initialConversationId)
        }
    }

    func selectConversation(id: String?) async {
        if let id,
           selectedConversationId == id,
           isCurrentConversationSelectionActive(id: id) {
            return
        }

        if id == nil || selectedConversationId != id {
            selectionRevision += 1
            olderMessagesLoadRevision += 1
            isLoadingOlderMessages = false
            loadedConversationDetailId = nil
        }

        streamErrorMessage = nil
        abortStreaming()
        selectedConversationId = id
        if let id {
            let revision = beginConversationDetailLoad()
            await ensureConversationLoaded(id: id)
            await loadConversation(id: id, revision: revision)
        } else {
            messages = []
            detailPageInfo = nil
            conversationTitleDraft = ""
            detailErrorMessage = nil
        }
    }

    func startNewConversation() async {
        await selectConversation(id: nil)
        draftMessage = ""
    }

    func beginConversationDetailLoad() -> Int {
        activeConversationDetailLoadRevision += 1
        return activeConversationDetailLoadRevision
    }

    func isCurrentConversationSelectionActive(id: String) -> Bool {
        loadedConversationDetailId == id ||
            activeConversationDetailLoadRevision != 0 ||
            streamingConversationId == id && isStreaming
    }
}
