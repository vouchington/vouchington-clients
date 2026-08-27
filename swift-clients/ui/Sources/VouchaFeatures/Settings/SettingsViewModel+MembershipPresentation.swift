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
        guard let membership, [.active, .pastDue].contains(membership.status) else {
            return "free"
        }
        return membership.plan
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
        case .active:
            return localized(
                membership.cancelAtPeriodEnd
                    ? .nativeSwiftMembershipActiveCancelsAtPeriodEnd
                    : .nativeSwiftMembershipActive
            )
        case .cancelled:
            return localized(.nativeSwiftMembershipCancelled)
        case .expired:
            return localized(.nativeSwiftMembershipExpired)
        case .pastDue:
            return localized(.nativeSwiftMembershipPastDue)
        case .paused:
            return localized(.nativeSwiftMembershipPaused)
        }
    }

    private func presentation(for plan: String) -> MembershipPlanPresentation {
        let isCurrent = currentMembershipPlanId == plan
        let title = membershipPlanTitle(plan)
        let sortedSkus = membershipPlans[plan, default: []].sorted { lhs, rhs in
            let lRank = membershipIntervalRank(lhs.interval)
            let rRank = membershipIntervalRank(rhs.interval)
            if lRank != rRank {
                return lRank < rRank
            }
            return lhs.price.amount < rhs.price.amount
        }
        let options = sortedSkus.map { summary in
            MembershipPlanPresentation.PriceOption(
                id: "\(summary.plan)-\(summary.interval)-\(summary.price.currency)-\(summary.price.amount)",
                priceText: formattedPrice(summary.price),
                intervalText: normalizedMembershipInterval(summary.interval)
            )
        }

        let priceSummaryText = priceSummary(plan: plan, options: options)

        let featureBullets = membershipFeatures(for: plan)
        let purchaseButtonTitle = purchaseTitle(for: plan, isCurrent: isCurrent)
        let purchaseButtonHint = purchaseHint(for: plan)
        let currentStateText = isCurrent
            ? currentMembershipStateText ?? localized(.nativeSwiftMembershipCurrentPlanPeriod)
            : nil

        return MembershipPlanPresentation(
            id: plan,
            title: title,
            currentStateText: currentStateText,
            priceSummaryText: priceSummaryText,
            priceOptions: options,
            featureBullets: featureBullets,
            purchaseButtonTitle: purchaseButtonTitle,
            purchaseButtonHint: purchaseButtonHint,
            isCurrent: isCurrent
        )
    }

    private func priceSummary(plan: String, options: [MembershipPlanPresentation.PriceOption]) -> String {
        if options.isEmpty {
            if plan == "free" {
                localized(.nativeSwiftMembershipFree)
            } else {
                localized(.nativeSwiftMembershipPriceUnavailable)
            }
        } else if let first = options.first, options.count == 1 {
            localized(
                .nativeSwiftMembershipPerPrice,
                parameters: ["price": first.priceText, "interval": summaryIntervalText(for: first.intervalText)]
            )
        } else {
            localized(
                .nativeSwiftMembershipStartingAt,
                parameters: ["price": options.first?.priceText ?? localized(.nativeSwiftMembershipPrice)]
            )
        }
    }

    private func formattedPrice(_ price: Money) -> String {
        guard price.amount > 0 else { return localized(.nativeSwiftMembershipFree) }
        guard let amount = price.knownCurrencyMajorUnitDecimal else {
            return localized(.nativeSwiftMembershipPriceUnavailable)
        }
        if let uiLocaleController {
            return uiLocaleController.currency(amount, code: price.currency.uppercased())
        }
        return UiMessages.currency(amount, code: price.currency.uppercased(), locale: .english)
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
