import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@MainActor
@Observable
final class CommunityAgentPromptWorkspace {
    var draftPrompt = ""
    var draftModelName = ""
    var draftModelProvider = ""
    var pagination = CursorPaginationState<CommunityAgentPrompt>()
    private(set) var slotInfo: CommunityAgentPromptSlotInfo?
    private(set) var isCreating = false
    private(set) var creationLimitReached = false
    private(set) var notice: UiVerbatimText?
    private let client: APIClient?
    private let slug: String
    private(set) var revision = 0

    init(client: APIClient?, slug: String) {
        self.client = client
        self.slug = slug
    }

    func invalidate() {
        revision += 1
        pagination.invalidateRequestsPreservingPage()
    }

    func reload(ifCurrent revision: Int) async {
        guard revision == self.revision else { return }
        await reload()
    }

    func reload() async {
        invalidate()
        pagination.reset()
        creationLimitReached = false
        notice = nil
        await fetchPage()
    }

    func loadMore() async {
        guard !isCreating else { return }
        await fetchPage()
    }

    private func fetchPage() async {
        guard let client, let request = pagination.beginNextPage() else { return }
        let revision = revision
        do {
            let response: CommunityAgentPromptsResponse = try await client.send(
                .communityAgentPrompts(idOrSlug: slug, after: request.cursor, limit: 25)
            )
            guard revision == self.revision else { return }
            guard !Task.isCancelled else {
                pagination.cancel(request)
                return
            }
            guard pagination.complete(
                request, items: response.communityAgentPrompts,
                endCursor: response.pageInfo.endCursor, hasNextPage: response.pageInfo.hasNextPage
            ) else { return }
            slotInfo = response.slotInfo
        } catch {
            guard revision == self.revision else { return }
            if error is CancellationError || Task.isCancelled {
                pagination.cancel(request)
            } else {
                pagination.fail(request, error: error as? VouchaError ?? .unexpected(error.localizedDescription))
            }
        }
    }

    @discardableResult
    func create(prompt: String, modelName: String? = nil, modelProvider: String? = nil) async -> Bool {
        let prompt = prompt.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !isCreating, !pagination.isLoading, !prompt.isEmpty, let client else { return false }
        revision += 1
        let revision = revision
        isCreating = true
        creationLimitReached = false
        notice = nil
        defer { isCreating = false }
        do {
            let _: EmptyResponse = try await client.send(.createCommunityAgentPrompt(
                idOrSlug: slug, prompt: prompt,
                modelName: modelName?.trimmedOrNil, modelProvider: modelProvider?.trimmedOrNil
            ))
            guard revision == self.revision, !Task.isCancelled else { return false }
            await reload()
            return revision + 1 == self.revision
        } catch {
            guard revision == self.revision, !Task.isCancelled else { return false }
            switch error as? VouchaError {
            case let .apiMessage(409, "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED", message):
                creationLimitReached = true
                notice = message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                    ? .message(.nativeSwiftCommunityStatusActionFailed) : .verbatim(message)
            case .api(409, "COMMUNITY_AGENT_PROMPT_LIMIT_REACHED"):
                creationLimitReached = true
                notice = .message(.nativeSwiftCommunityStatusActionFailed)
            default:
                notice = .message(.nativeSwiftCommunityStatusActionFailed)
            }
            return false
        }
    }
}
