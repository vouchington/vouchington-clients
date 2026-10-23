import Foundation
import VouchaLocalization
import VouchaModels

public struct MembershipPlanPresentation: Identifiable, Equatable, Sendable {
    public struct PriceOption: Identifiable, Equatable, Sendable {
        public let id: String
        public let priceText: String
        public let intervalText: String

        public init(id: String, priceText: String, intervalText: String) {
            self.id = id
            self.priceText = priceText
            self.intervalText = intervalText
        }
    }

    public let id: String
    public let title: String
    public let currentStateText: String?
    public let priceSummaryText: String
    public let priceOptions: [PriceOption]
    public let featureBullets: [String]
    public let purchaseButtonTitle: String
    public let purchaseButtonHint: String
    public let isCurrent: Bool

    public init(
        id: String,
        title: String,
        currentStateText: String?,
        priceSummaryText: String,
        priceOptions: [PriceOption],
        featureBullets: [String],
        purchaseButtonTitle: String,
        purchaseButtonHint: String,
        isCurrent: Bool
    ) {
        self.id = id
        self.title = title
        self.currentStateText = currentStateText
        self.priceSummaryText = priceSummaryText
        self.priceOptions = priceOptions
        self.featureBullets = featureBullets
        self.purchaseButtonTitle = purchaseButtonTitle
        self.purchaseButtonHint = purchaseButtonHint
        self.isCurrent = isCurrent
    }
}

public extension SettingsViewModel {
    var currentMembershipPlanId: String {
        membership?.plan ?? "free"
    }

    var membershipSummaryText: String {
        let plan = membershipPlanTitle(currentMembershipPlanId)
        let stateText = currentMembershipStateText ?? localized(.nativeSwiftMembershipNoActiveMembership)
        return localized(
            .nativeSwiftMembershipCurrentPlanSummary,
            parameters: ["plan": plan, "state": stateText]
        )
    }

    var membershipPlanPresentations: [MembershipPlanPresentation] {
        let planOrder = ["free", "plus", "pro"]
        return planOrder.map { plan in
            presentation(for: plan)
        }
    }

    var currentMembershipStateText: String? {
        guard let membership else {
            return currentMembershipPlanId == "free" ? nil : localized(.nativeSwiftMembershipNoActiveMembership)
        }
        switch membership.status {
        case "active":
            return localized(
                membership.cancelAtPeriodEnd == true
                    ? .nativeSwiftMembershipActiveCancelsAtPeriodEnd
                    : .nativeSwiftMembershipActive
            )
        case "cancelled":
            return localized(.nativeSwiftMembershipCancelled)
        case "expired":
            return localized(.nativeSwiftMembershipExpired)
        case "past_due":
            return localized(.nativeSwiftMembershipPastDue)
        case "paused":
            return localized(.nativeSwiftMembershipPaused)
        default:
            return localized(.nativeSwiftMembershipNativeBillingUnavailable)
        }
    }

    private func presentation(for plan: String) -> MembershipPlanPresentation {
        let isCurrent = currentMembershipPlanId == plan
        let priceSummaryText = plan == "free"
            ? localized(.nativeSwiftMembershipFree)
            : paidPriceSummary(showsStoreSchedule: false)
        return MembershipPlanPresentation(
            id: plan,
            title: membershipPlanTitle(plan),
            currentStateText: isCurrent
                ? currentMembershipStateText ?? localized(.nativeSwiftMembershipCurrentPlanPeriod)
                : nil,
            priceSummaryText: priceSummaryText,
            priceOptions: storePriceOptions(for: plan, showsStoreSchedule: false),
            featureBullets: membershipFeatures(for: plan),
            purchaseButtonTitle: purchaseTitle(for: plan, isCurrent: isCurrent),
            purchaseButtonHint: purchaseHint(for: plan),
            isCurrent: isCurrent
        )
    }

    private func paidPriceSummary(showsStoreSchedule: Bool) -> String {
        guard showsStoreSchedule else {
            return localized(.nativeSwiftMembershipPriceUnavailable)
        }
        return localized(
            .nativeSwiftMembershipStartingAt,
            parameters: ["price": localized(.nativeSwiftMembershipPrice)]
        )
    }

    private func storePriceOptions(
        for plan: String,
        showsStoreSchedule: Bool
    ) -> [MembershipPlanPresentation.PriceOption] {
        guard showsStoreSchedule, plan != "free" else { return [] }
        return membershipPlans[plan, default: []].map { sku in
            MembershipPlanPresentation.PriceOption(
                id: sku.id,
                priceText: localized(.nativeSwiftMembershipPriceUnavailable),
                intervalText: normalizedMembershipInterval(sku.interval)
            )
        }
    }

    private func purchaseTitle(for plan: String, isCurrent: Bool) -> String {
        if isCurrent {
            return localized(.nativeSwiftMembershipCurrentPlan)
        }
        if plan == "free" {
            return localized(.nativeSwiftMembershipIncluded)
        }
        return localized(.nativeSwiftMembershipNativeBillingPending)
    }

    private func purchaseHint(for plan: String) -> String {
        if plan == "free" {
            return localized(.nativeSwiftMembershipFreeAccessIncluded)
        }
        return localized(.nativeSwiftMembershipNativeBillingUnavailable)
    }

    private func membershipPlanTitle(_ plan: String) -> String {
        localized(membershipPlanText(plan))
    }

}
