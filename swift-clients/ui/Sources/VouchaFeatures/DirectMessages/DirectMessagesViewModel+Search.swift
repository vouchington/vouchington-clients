import Foundation
import VouchaAPI
import VouchaModels

/// Safety bound on additional pages fetched per search. A client-side exclusion filter
/// (already-selected recipients) can consume an entire page of results, which would
/// otherwise render an empty dropdown despite real matches existing further in the cursor.
/// This caps the fan-out from a single search while still following `page_info` instead of
/// silently truncating at page one.
private let userSearchPageLimit = 10
private let userSearchMaxAdditionalPages = 4

@MainActor
public extension DirectMessagesViewModel {
    func searchUsers(query: String, excludeIds: Set<String> = []) async {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        nextUserSearchRequestID += 1
        let requestID = nextUserSearchRequestID
        guard !trimmed.isEmpty else {
            userResults = []
            return
        }
        let matches = await matchingUsers(query: trimmed, excludeIds: excludeIds) {
            requestID == self.nextUserSearchRequestID
        }
        guard requestID == nextUserSearchRequestID else { return }
        userResults = matches
    }

    func searchParticipantUsers(query: String) async {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        nextParticipantSearchRequestID += 1
        let requestID = nextParticipantSearchRequestID
        guard !trimmed.isEmpty else {
            participantUserResults = []
            return
        }
        let matches = await matchingUsers(query: trimmed, excludeIds: []) {
            requestID == self.nextParticipantSearchRequestID
        }
        guard requestID == nextParticipantSearchRequestID else { return }
        participantUserResults = matches
    }
}

@MainActor
extension DirectMessagesViewModel {
    func clearParticipantSearch() {
        nextParticipantSearchRequestID += 1
        participantUserResults = []
    }

    /// Searches users and filters out already-selected/excluded ids client-side, following
    /// `page_info.has_next_page` until there are enough visible matches, the result set is
    /// exhausted, or the page-count safety bound is hit. `isCurrent` guards against a stale
    /// generation continuing to fetch after a newer search superseded it.
    private func matchingUsers(
        query: String,
        excludeIds: Set<String>,
        isCurrent: () -> Bool
    ) async -> [PublicUser] {
        var matches: [PublicUser] = []
        var after: String?
        for _ in 0 ... userSearchMaxAdditionalPages {
            guard isCurrent() else { return matches }
            guard let page: Page<PublicUser> = try? await client.send(
                .myMessageUserSearch(query: query, after: after, limit: userSearchPageLimit)
            ) else { return matches }
            for user in page.results where !excludeIds.contains(user.id) {
                matches.append(user)
            }
            guard matches.count < userSearchPageLimit,
                  page.pageInfo.hasNextPage,
                  let endCursor = page.pageInfo.endCursor else {
                break
            }
            after = endCursor
        }
        return matches
    }
}
