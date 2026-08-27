import VouchaLocalization
import VouchaModels

extension SettingsViewModel {
    func membershipFeatures(for plan: String) -> [String] {
        guard let catalog = membershipBenefitCatalog else { return legacyMembershipFeatures(for: plan) }
        return catalog.groups.flatMap(\.benefits).compactMap { benefit in
            guard benefit.placements.contains("card"),
                  let labelKey = membershipBenefitLabelKey(benefit.id),
                  let value = benefit.values[plan],
                  membershipBenefitIsIncluded(value),
                  let valueText = membershipBenefitValueText(value)
            else { return nil }
            return "\(localized(labelKey)): \(valueText)"
        }
    }

    private func legacyMembershipFeatures(for plan: String) -> [String] {
        switch plan {
        case "plus":
            [
                localized(.extractedMembershipsBenefitCatalogImmediate0c11c1ba),
                localized(.extractedMembershipsBenefitCatalogMore0c11c1bd)
            ]
        case "pro":
            [
                localized(.extractedMembershipsBenefitCatalogImmediate0c11c1ba),
                localized(.extractedMembershipsBenefitCatalogMost0c11c1be)
            ]
        default: []
        }
    }

    private func membershipBenefitLabelKey(_ id: String) -> UiMessageKey? {
        [
            "public_contribution_access": .extractedMembershipsBenefitCatalogAccess0c11c1a5,
            "contribution_capacity": .extractedMembershipsBenefitCatalogContributionCapacity0c11c1a7,
            "automatic_post_topics": .extractedMembershipsBenefitCatalogAutomaticPostTopics0c11c1ab,
            "ugc_downvote_counts": .extractedMembershipsBenefitCatalogDownvoteCounts0c11c1b1,
            "community_agent_rules": .extractedMembershipsBenefitCatalogCommunityAgentRules0c11c1b5,
            "support_service_level": .extractedMembershipsBenefitCatalogSupportServiceLevel0c11c1c1
        ][id]
    }

    private func membershipBenefitValueText(_ value: MembershipBenefitValue) -> String? {
        if let key = MembershipBenefitPresentation.messageKey(for: value) {
            return localized(key)
        }
        return membershipBenefitQuantityText(value)
    }

    private func membershipBenefitQuantityText(_ value: MembershipBenefitValue) -> String? {
        guard value.kind == "quantity",
              let quantity = value.quantity,
              quantity >= 0
        else { return nil }
        return quantity == 0
            ? localized(.extractedMembershipsBenefitCatalogNone0c11c1bb)
            : String(quantity)
    }

    func summaryIntervalText(for intervalText: String) -> String {
        switch intervalText.lowercased() {
        case "monthly", "month":
            localized(.nativeSwiftMembershipMonth)
        case "yearly", "year":
            localized(.nativeSwiftMembershipYear)
        default:
            intervalText.lowercased()
        }
    }

    func membershipIntervalRank(_ interval: String) -> Int {
        switch interval.lowercased() {
        case let value where value.hasPrefix("month"):
            0
        case let value where value.hasPrefix("year"):
            1
        default:
            2
        }
    }

    func normalizedMembershipInterval(_ interval: String) -> String {
        switch interval.lowercased() {
        case let value where value.hasPrefix("month"):
            localized(.nativeSwiftMembershipMonthly)
        case let value where value.hasPrefix("year"):
            localized(.nativeSwiftMembershipYearly)
        default:
            localized(.verbatim(interval))
        }
    }
}

private func membershipBenefitIsIncluded(_ value: MembershipBenefitValue) -> Bool {
    switch value.kind {
    case "availability": value.included == true
    case "level": value.level != "none"
    case "quantity": (value.quantity ?? 0) > 0
    default: true
    }
}

private enum MembershipBenefitPresentation {
    private static let accessKeys: [String: UiMessageKey] = [
        "immediate": .extractedMembershipsBenefitCatalogImmediate0c11c1ba,
        "after_wait": .extractedMembershipsBenefitCatalogAfterWait0c11c1b9
    ]

    private static let levelKeys: [String: UiMessageKey] = [
        "none": .extractedMembershipsBenefitCatalogNone0c11c1bb,
        "standard": .extractedMembershipsBenefitCatalogStandard0c11c1bc,
        "more": .extractedMembershipsBenefitCatalogMore0c11c1bd,
        "most": .extractedMembershipsBenefitCatalogMost0c11c1be,
        "higher": .extractedMembershipsBenefitCatalogHigher0c11c1bf,
        "priority": .extractedMembershipsBenefitCatalogPriority0c11c1c3,
        "highest_priority": .extractedMembershipsBenefitCatalogHighestPriority0c11c1c4
    ]

    static func messageKey(for value: MembershipBenefitValue) -> UiMessageKey? {
        switch value.kind {
        case "availability":
            guard let included = value.included else { return nil }
            return included
                ? .extractedMembershipsPlanComparisonTableIncludedBa829a98
                : .extractedMembershipsPlanComparisonTableNotIncludedB665bfc2
        case "access":
            return accessKeys[value.access ?? ""]
        case "level":
            return levelKeys[value.level ?? ""]
        default:
            return nil
        }
    }
}
