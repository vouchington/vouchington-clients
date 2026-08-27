import Foundation
import Observation
import VouchaAPI
import VouchaCore
import VouchaLocalization
import VouchaModels

@Observable
@MainActor
final class MembershipGrantViewModel {
    let client: APIClient?
    var query = ""
    var candidates: [MembershipGrantUser] = []
    var selectedUser: MembershipGrantUser?
    var selectedPlan: MembershipPlanSlug?
    var selectedSkuId: String?
    var plans: [String: [MembershipSkuSummary]] = [:]
    var isLoadingPlans = false
    var isSearching = false
    var isSubmitting = false
    var plansError: UiVerbatimText?
    var searchError: UiVerbatimText?
    var submissionMessage: UiVerbatimText?
    private var didLoadPlans = false
    var searchGeneration = 0

    init(client: APIClient?) {
        self.client = client
    }

    var availableSkus: [MembershipSkuSummary] {
        guard let selectedPlan else { return [] }
        return plans[selectedPlan.rawValue, default: []].filter { $0.plan == selectedPlan.rawValue }
    }

    var selectedSku: MembershipSkuSummary? {
        availableSkus.first { $0.id == selectedSkuId }
    }

    var canSubmit: Bool {
        hasValidSelection && !isSubmitting && !isLoadingPlans
    }

    func loadPlans(force: Bool = false) async {
        guard force || !didLoadPlans, !isLoadingPlans, let client else { return }
        isLoadingPlans = true
        plansError = nil
        defer { isLoadingPlans = false }
        do {
            let response: MembershipPlansResponse = try await client.send(.membershipPlans)
            plans = response.plans
            didLoadPlans = true
            if selectedSkuId != nil, selectedSku == nil {
                selectedSkuId = nil
            }
        } catch {
            didLoadPlans = false
            plansError = .message(.nativeSwiftMembershipPlansLoadFailure)
        }
    }

    func updateQuery(_ value: String) {
        query = value
        searchGeneration += 1
        selectedUser = nil
        candidates = []
        searchError = nil
        isSearching = false
    }

    func selectUser(_ user: MembershipGrantUser) {
        searchGeneration += 1
        selectedUser = user
        candidates = []
        searchError = nil
        isSearching = false
    }

    func selectPlan(_ plan: MembershipPlanSlug?) {
        selectedPlan = plan
        selectedSkuId = nil
    }

    func grant() async {
        guard canSubmit,
              let client,
              let selectedUserId = selectedUser?.id,
              let selectedPlan,
              let selectedSkuId
        else {
            submissionMessage = .message(.nativeSwiftMembershipMembershipGrantValidation)
            return
        }
        isSubmitting = true
        submissionMessage = nil
        defer { isSubmitting = false }
        await loadPlans(force: true)
        guard plansError == nil else {
            return
        }
        guard refreshedCatalogContains(plan: selectedPlan, skuId: selectedSkuId) else {
            submissionMessage = .message(.nativeSwiftMembershipMembershipGrantValidation)
            return
        }
        do {
            let _: MembershipGrantResponse = try await client.send(
                .grantMembership(userId: selectedUserId, plan: selectedPlan, skuId: selectedSkuId)
            )
            query = ""
            candidates = []
            selectedUser = nil
            self.selectedPlan = nil
            self.selectedSkuId = nil
            searchGeneration += 1
            submissionMessage = .message(.nativeSwiftMembershipMembershipGrantSuccess)
        } catch {
            submissionMessage = grantFailureMessage(for: error)
        }
    }

    private func grantFailureMessage(for error: Error) -> UiVerbatimText {
        guard let apiError = error as? VouchaError,
              case let .apiMessage(statusCode, _, message) = apiError,
              (400 ..< 500).contains(statusCode),
              !message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        else {
            return .message(.nativeSwiftMembershipMembershipGrantFailure)
        }
        return .userContent(message)
    }

    private var hasValidSelection: Bool {
        selectedUser != nil && selectedPlan != nil && selectedSku != nil
    }

    private func refreshedCatalogContains(plan: MembershipPlanSlug, skuId: String) -> Bool {
        plans[plan.rawValue, default: []].contains { sku in
            sku.id == skuId && sku.plan == plan.rawValue
        }
    }
}
