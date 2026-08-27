import Foundation
import VouchaLocalization
import VouchaModels

extension HouseholdViewModel {
    func createHousehold() async {
        guard canCreateHousehold else { return }
        isCreating = true
        mutationErrorMessage = nil
        defer { isCreating = false }
        do {
            let household = try await service.createHousehold()
            guard ownedHousehold == nil else { return }
            hasConfirmedNoOwnedHousehold = false
            ownedHousehold = household
            sections.insert(HouseholdMembershipSection(
                household: household,
                members: [],
                isLoading: false,
                errorMessage: nil,
                isOwned: true
            ), at: 0)
            await loadMemberships(householdId: household.id)
        } catch {
            mutationErrorMessage = .message(.nativeSwiftHouseholdsBookmarksHouseholdCreateFailed)
        }
    }

    func removeMembership(_ membership: HouseholdMembership, householdId: String) async {
        guard !removingMembershipIds.contains(membership.id),
              let sectionIndex = sections.firstIndex(where: { $0.id == householdId && $0.isOwned }),
              let memberIndex = sections[sectionIndex].members.firstIndex(where: { $0.id == membership.id })
        else { return }

        removingMembershipIds.insert(membership.id)
        mutationErrorMessage = nil
        sections[sectionIndex].invalidateForMembershipMutation()
        guard sections[sectionIndex].removeOptimistically(membership.id) != nil else {
            removingMembershipIds.remove(membership.id)
            return
        }
        defer {
            removingMembershipIds.remove(membership.id)
        }
        do {
            try await service.removeMembership(householdId: householdId, membershipId: membership.id)
            removedMembershipIds.insert(membership.id)
        } catch {
            mutationErrorMessage = .message(.nativeSwiftHouseholdsBookmarksHouseholdRemoveFailed)
            guard let currentSection = sections.firstIndex(where: { $0.id == householdId }),
                  !sections[currentSection].members.contains(where: { $0.id == membership.id })
            else { return }
            sections[currentSection].restore(membership, fallbackIndex: memberIndex)
        }
    }
}
