import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class NativeSupportViewModel {
    let client: APIClient?
    private let initialThreadId: String?

    var threadPagination = CursorPaginationState<SupportThread>()
    var threads: [SupportThread] {
        get { threadPagination.items }
        set { threadPagination.replaceItems(newValue) }
    }

    var listPageInfo: NativePaginationState? {
        get {
            guard threadPagination.hasLoadedPage else { return nil }
            return .init(hasNextPage: threadPagination.hasMore, endCursor: threadPagination.endCursor)
        }
        set {
            guard let newValue else {
                threadPagination.reset(items: threads)
                return
            }
            threadPagination.restoreContinuation(endCursor: newValue.endCursor, hasMore: newValue.hasNextPage)
        }
    }

    var selectedThreadId: String?
    var selectedThreadMessages: [SupportMessage] = []
    var selectedThreadPageInfo: NativePaginationState?
    var subject = ""
    var message = ""
    var conversationId = ""
    var listErrorMessage: UiVerbatimText?
    var detailErrorMessage: UiVerbatimText?
    var createErrorMessage: UiVerbatimText?
    var isLoadingList = false
    @ObservationIgnored
    var activeThreadListLoadRevision = 0
    var isLoadingDetail = false
    var detailLoadCount = 0
    private var isLoadingMoreSelectedThreadMessages = false
    var isCreating = false

    init(client: APIClient?, routeMatch: NativeRouteMatch?) {
        self.client = client
        initialThreadId = routeMatch?.param("threadId")
        selectedThreadId = initialThreadId
        conversationId = routeMatch?.queryValue("conversation_id") ?? ""
    }

    var selectedThread: SupportThread? {
        guard let selectedThreadId else { return nil }
        return threads.first { $0.id == selectedThreadId }
    }

    var canLoadMoreThreads: Bool {
        threadPagination.hasMore && !threadPagination.isLoading
    }

    var canLoadMoreSelectedThreadMessages: Bool {
        selectedThreadPageInfo?.hasNextPage == true && isLoadingMoreSelectedThreadMessages == false
    }
}

private struct NativeSupportCreateRequestState {
    let selectedThreadId: String?
    let subject: String
    let message: String
    let conversationId: String
}

private extension NativeSupportViewModel {
    func currentCreateRequestState() -> NativeSupportCreateRequestState {
        .init(
            selectedThreadId: selectedThreadId,
            subject: subject,
            message: message,
            conversationId: conversationId
        )
    }

    func isCurrentCreateRequest(_ state: NativeSupportCreateRequestState) -> Bool {
        selectedThreadId == state.selectedThreadId &&
            subject == state.subject &&
            message == state.message &&
            conversationId == state.conversationId
    }
}

extension NativeSupportViewModel {
    func load() async {
        await loadThreads(reset: true)
        if let initialThreadId {
            await loadThread(id: initialThreadId)
        }
    }

    func loadMoreThreads() async {
        await loadThreads(reset: false)
    }

    func loadMoreSelectedThreadMessages() async {
        guard canLoadMoreSelectedThreadMessages,
              let id = selectedThreadId,
              let cursor = selectedThreadPageInfo?.endCursor,
              !isLoadingMoreSelectedThreadMessages
        else {
            return
        }
        isLoadingMoreSelectedThreadMessages = true
        defer { isLoadingMoreSelectedThreadMessages = false }
        await loadThreadMessages(id: id, after: cursor, append: true)
    }

    func selectThread(id: String?) async {
        selectedThreadId = id
        if let id {
            selectedThreadMessages = []
            selectedThreadPageInfo = nil
            detailErrorMessage = nil
            await loadThread(id: id)
        } else {
            selectedThreadMessages = []
            selectedThreadPageInfo = nil
            detailErrorMessage = nil
        }
    }

    func startNewThread() async {
        await selectThread(id: nil)
        subject = ""
        message = ""
        conversationId = ""
    }

    func createThread() async {
        guard let client else { return }
        guard isCreating == false else { return }
        let normalizedSubject = subject.trimmingCharacters(in: .whitespacesAndNewlines)
        guard normalizedSubject.isEmpty == false else {
            createErrorMessage = .message(.nativeSwiftValidationSubjectRequired)
            return
        }

        let requestState = currentCreateRequestState()
        isCreating = true
        createErrorMessage = nil
        defer { isCreating = false }
        do {
            let response: CreateSupportThreadResponse = try await client.send(
                .createSupportThread(
                    subject: normalizedSubject,
                    message: message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : message,
                    conversationId: conversationId.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                        ? nil
                        : conversationId
                )
            )
            guard isCurrentCreateRequest(requestState) else { return }
            threads.insert(response.thread, at: 0)
            selectedThreadId = response.thread.id
            subject = ""
            message = ""
            conversationId = ""
            await loadThread(id: response.thread.id)
        } catch let error as VouchaError {
            guard isCurrentCreateRequest(requestState) else { return }
            createErrorMessage = error.errorDescription.map(UiVerbatimText.verbatim)
        } catch {
            guard isCurrentCreateRequest(requestState) else { return }
            createErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}
