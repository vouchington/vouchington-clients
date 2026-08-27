import Foundation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

/// Mirrors `DirectMessagesViewModel`'s bounded page-following: caps fan-out from a single
/// search while still following `page_info` instead of silently truncating at page one.
private let membershipGrantSearchPageLimit = 10
private let membershipGrantSearchMaxAdditionalPages = 4

@MainActor
extension MembershipGrantViewModel {
    func search() async {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        searchGeneration += 1
        let generation = searchGeneration
        selectedUser = nil
        candidates = []
        searchError = nil
        isSearching = false
        guard !trimmed.isEmpty, let client else { return }
        isSearching = true
        defer {
            if generation == searchGeneration {
                isSearching = false
            }
        }
        let result = await matchingCandidates(query: trimmed, client: client, generation: generation)
        guard generation == searchGeneration else { return }
        switch result {
        case let .success(matches):
            candidates = matches
        case .failure:
            searchError = .message(.nativeSwiftMembershipSearchFailure)
        }
    }

    /// Follows `page_info.has_next_page` until there are enough matches, the result set is
    /// exhausted, or the page-count safety bound is hit. Returns `.failure` only when the
    /// very first page fails; a mid-loop failure just stops early with whatever was found.
    private func matchingCandidates(
        query: String,
        client: APIClient,
        generation: Int
    ) async -> Result<[MembershipGrantUser], VouchaError> {
        var matches: [MembershipGrantUser] = []
        var after: String?
        for page in 0 ... membershipGrantSearchMaxAdditionalPages {
            guard generation == searchGeneration else { return .success(matches) }
            do {
                let response: Page<MembershipGrantUser> = try await client.send(
                    .membershipGrantUserSearch(query: query, after: after, limit: membershipGrantSearchPageLimit)
                )
                matches.append(contentsOf: response.results)
                guard matches.count < membershipGrantSearchPageLimit,
                      response.pageInfo.hasNextPage,
                      let endCursor = response.pageInfo.endCursor else {
                    break
                }
                after = endCursor
            } catch {
                guard page == 0 else { break }
                return .failure(error as? VouchaError ?? .api(statusCode: 0, preconditionCode: nil))
            }
        }
        return .success(matches)
    }
}
