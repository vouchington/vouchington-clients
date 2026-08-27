import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class HouseholdViewModel {
    var ownedHousehold: Household?
    var memberHouseholdPagination = CursorPaginationState<Household>()
    var sections: [HouseholdMembershipSection] = []
    var isCreating = false
    var errorMessage: UiVerbatimText?
    var mutationErrorMessage: UiVerbatimText?
    var removingMembershipIds: Set<String> = []

    let service: any HouseholdServicing
    var hasConfirmedNoOwnedHousehold = false
    private var ownedLoadGeneration = UUID()
    private var isLoadingOwnedHousehold = false
    var removedMembershipIds: Set<String> = []

    var memberOnlyHouseholds: [Household] {
        memberHouseholdPagination.items
    }

    var isLoading: Bool {
        isLoadingOwnedHousehold
            || (memberHouseholdPagination.isLoading && !memberHouseholdPagination.hasLoadedPage)
    }

    var hasMoreMemberHouseholds: Bool {
        memberHouseholdPagination.hasMore
    }

    var isLoadingMemberHouseholds: Bool {
        memberHouseholdPagination.isLoading
    }

    var hasMemberHouseholdPaginationError: Bool {
        memberHouseholdPagination.lastError != nil
    }

    var canCreateHousehold: Bool {
        hasConfirmedNoOwnedHousehold && ownedHousehold == nil && !isLoadingOwnedHousehold && !isCreating
    }

    init(service: any HouseholdServicing) {
        self.service = service
    }

    func load() async {
        let ownedGeneration = UUID()
        ownedLoadGeneration = ownedGeneration
        hasConfirmedNoOwnedHousehold = false
        isLoadingOwnedHousehold = true
        errorMessage = nil
        memberHouseholdPagination.reset()
        removedMembershipIds = []
        for index in sections.indices {
            sections[index].resetForReload()
        }

        async let ownedLoad: Void = loadOwnedHousehold(generation: ownedGeneration)
        async let memberLoad: Void = loadNextMemberHouseholds()
        _ = await (ownedLoad, memberLoad)
    }

    func loadNextMemberHouseholds() async {
        guard let request = memberHouseholdPagination.beginNextPage() else { return }
        do {
            let page = try await service.households(access: .member, after: request.cursor, limit: 25)
            guard memberHouseholdPagination.isCurrent(request) else { return }
            let previousIds = Set(memberHouseholdPagination.items.map(\.id))
            memberHouseholdPagination.complete(
                request,
                items: page.results,
                endCursor: page.pageInfo.endCursor,
                hasNextPage: page.pageInfo.hasNextPage
            )
            let addedIds = Set(memberHouseholdPagination.items.map(\.id)).subtracting(previousIds)
            reconcileSections()
            await loadInitialMemberships(householdIds: addedIds)
        } catch {
            if Task.isCancelled {
                memberHouseholdPagination.cancel(request)
            } else {
                memberHouseholdPagination.fail(request, error: error.asVouchaError)
            }
        }
    }

    func loadMemberships(householdId: String) async {
        guard let index = sections.firstIndex(where: { $0.id == householdId }),
              let request = sections[index].beginInitialPageIfNeeded()
        else { return }
        await loadMembershipPage(householdId: householdId, request: request)
    }

    func loadNextMemberships(householdId: String) async {
        guard let index = sections.firstIndex(where: { $0.id == householdId }),
              let request = sections[index].beginNextPage()
        else { return }
        await loadMembershipPage(householdId: householdId, request: request)
    }

    private func loadOwnedHousehold(generation: UUID) async {
        do {
            let page = try await service.households(access: .owned, after: nil, limit: 1)
            guard ownedLoadGeneration == generation else { return }
            isLoadingOwnedHousehold = false
            ownedHousehold = page.results.first
            hasConfirmedNoOwnedHousehold = page.results.isEmpty
            reconcileSections()
            if let householdId = ownedHousehold?.id {
                await loadMemberships(householdId: householdId)
            }
        } catch {
            guard ownedLoadGeneration == generation else { return }
            isLoadingOwnedHousehold = false
            ownedHousehold = nil
            hasConfirmedNoOwnedHousehold = false
            errorMessage = .verbatim(error.householdMessage)
            reconcileSections()
        }
    }

    private func loadMembershipPage(householdId: String, request: CursorPageRequest) async {
        do {
            let page = try await service.memberships(householdId: householdId, after: request.cursor, limit: 25)
            guard let index = sections.firstIndex(where: { $0.id == householdId }),
                  sections[index].isCurrent(request)
            else { return }
            sections[index].complete(
                request,
                page: page,
                excluding: removingMembershipIds.union(removedMembershipIds)
            )
        } catch {
            guard let index = sections.firstIndex(where: { $0.id == householdId }) else { return }
            sections[index].fail(request, error: error)
        }
    }

    private func loadInitialMemberships(householdIds: Set<String>) async {
        await withTaskGroup(of: Void.self) { group in
            for householdId in householdIds {
                group.addTask { await self.loadMemberships(householdId: householdId) }
            }
        }
    }

    private var displayedHouseholds: [Household] {
        [ownedHousehold].compactMap { $0 } + memberOnlyHouseholds
    }

    private func reconcileSections() {
        let previous = Dictionary(uniqueKeysWithValues: sections.map { ($0.id, $0) })
        sections = displayedHouseholds.map { household in
            guard var existing = previous[household.id] else {
                return HouseholdMembershipSection(
                    household: household,
                    members: [],
                    isLoading: false,
                    errorMessage: nil,
                    isOwned: household.id == ownedHousehold?.id
                )
            }
            existing.household = household
            return existing
        }
    }
}
