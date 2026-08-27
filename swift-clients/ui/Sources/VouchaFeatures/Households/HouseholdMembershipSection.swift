import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

struct HouseholdMembershipSection: Identifiable {
    var household: Household
    private(set) var pagination: CursorPaginationState<HouseholdMembership>
    var errorMessage: UiVerbatimText?
    let isOwned: Bool
    var memberOrder: [String: Int] = [:]

    init(
        household: Household,
        members: [HouseholdMembership],
        isLoading _: Bool,
        errorMessage: UiVerbatimText?,
        isOwned: Bool,
        memberOrder: [String: Int] = [:]
    ) {
        self.household = household
        if members.isEmpty {
            pagination = CursorPaginationState()
        } else {
            var loadedPagination = CursorPaginationState<HouseholdMembership>()
            if let request = loadedPagination.beginInitialPageIfNeeded() {
                loadedPagination.complete(request, items: members, endCursor: nil, hasNextPage: false)
            }
            pagination = loadedPagination
        }
        self.errorMessage = errorMessage
        self.isOwned = isOwned
        self.memberOrder = memberOrder
        if errorMessage != nil, let request = pagination.beginNextPage() {
            pagination.fail(request, error: .api(statusCode: 0, preconditionCode: nil))
        }
    }

    var id: String {
        household.id
    }

    var members: [HouseholdMembership] {
        get { pagination.items }
        set { pagination.replaceItems(newValue) }
    }

    var isLoading: Bool {
        pagination.isLoading
    }

    var hasMore: Bool {
        pagination.hasMore
    }

    var hasPaginationError: Bool {
        pagination.lastError != nil
    }

    mutating func beginInitialPageIfNeeded() -> CursorPageRequest? {
        pagination.beginInitialPageIfNeeded()
    }

    mutating func beginNextPage() -> CursorPageRequest? {
        pagination.beginNextPage()
    }

    func isCurrent(_ request: CursorPageRequest) -> Bool {
        pagination.isCurrent(request)
    }

    mutating func complete(
        _ request: CursorPageRequest,
        page: HouseholdMembershipPage,
        excluding excludedMembershipIds: Set<String>
    ) {
        guard pagination.complete(
            request,
            items: page.results,
            endCursor: page.pageInfo.endCursor,
            hasNextPage: page.pageInfo.hasNextPage
        ) else { return }
        pagination.remove { excludedMembershipIds.contains($0.id) }
        var nextRank = (memberOrder.values.max() ?? -1) + 1
        for membership in page.results where memberOrder[membership.id] == nil {
            memberOrder[membership.id] = nextRank
            nextRank += 1
        }
        errorMessage = nil
    }

    mutating func fail(_ request: CursorPageRequest, error: Error) {
        let vouchaError = error.asVouchaError
        guard pagination.fail(request, error: vouchaError) else { return }
        errorMessage = .verbatim(error.householdMessage)
    }

    mutating func resetForReload() {
        pagination.reset()
        memberOrder = [:]
        errorMessage = nil
    }

    mutating func invalidateForMembershipMutation() {
        pagination.reset(items: pagination.items)
        errorMessage = nil
    }

    mutating func applyMembers(
        _ memberships: [HouseholdMembership],
        excluding excludedMembershipIds: Set<String> = []
    ) {
        var seenMembershipIds = Set<String>()
        let normalizedMemberships = memberships.filter { membership in
            seenMembershipIds.insert(membership.id).inserted
        }
        pagination.replaceItems(normalizedMemberships.filter { !excludedMembershipIds.contains($0.id) })
        memberOrder = Dictionary(uniqueKeysWithValues: normalizedMemberships.enumerated().map { ($1.id, $0) })
        errorMessage = nil
    }

    mutating func removeOptimistically(_ membershipId: String) -> Int? {
        if memberOrder.isEmpty {
            memberOrder = Dictionary(uniqueKeysWithValues: members.enumerated().map { ($1.id, $0) })
        }
        guard let index = members.firstIndex(where: { $0.id == membershipId }) else { return nil }
        members.remove(at: index)
        return index
    }

    mutating func restore(_ membership: HouseholdMembership, fallbackIndex: Int) {
        guard !members.contains(where: { $0.id == membership.id }) else { return }
        let rank = memberOrder[membership.id]
        let rankedIndex = rank.flatMap { memberRank in
            members.firstIndex { memberOrder[$0.id, default: .max] > memberRank }
        }
        members.insert(membership, at: rankedIndex ?? min(fallbackIndex, members.count))
    }
}

extension Error {
    var asVouchaError: VouchaError {
        self as? VouchaError ?? .api(statusCode: 0, preconditionCode: nil)
    }

    var householdMessage: String {
        (self as? VouchaError)?.errorDescription ?? localizedDescription
    }
}

extension HouseholdMembership {
    var householdDisplayName: UiVerbatimText {
        guard let username = individual.username?.trimmingCharacters(in: .whitespacesAndNewlines),
              !username.isEmpty
        else {
            return .message(.nativeSwiftHouseholdsBookmarksHouseholdMember)
        }
        return .userContent("@\(username)")
    }

    var householdDisplayRelationship: UiVerbatimText? {
        guard let value = relationship?.trimmingCharacters(in: .whitespacesAndNewlines), !value.isEmpty else {
            return nil
        }
        return .userContent(value)
    }
}
