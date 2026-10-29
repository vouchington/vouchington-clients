import VouchaLocalization
import VouchaModels

extension SettingsViewModel {
    var membershipLifecycleLineTexts: [String] {
        guard let membershipOverview else { return [] }
        return membershipLifecycleLines(membershipOverview)
    }

    func noteMembershipOverviewStale() {
        guard membershipOverview != nil else { return }
        membershipSnapshotIsStale = true
    }

    private func membershipLifecycleLines(_ overview: NativeMembershipOverview) -> [String] {
        var lines: [String] = []
        if membershipSnapshotIsStale {
            lines.append(localized(.nativeSwiftMembershipNativeBillingUnavailable))
        }
        lines.append(contentsOf: sourceLines(overview))
        lines.append(contentsOf: switchLines(overview))
        lines.append(contentsOf: grantLines(overview))
        lines.append(contentsOf: operationLines(overview))
        lines.append(contentsOf: verificationLines(overview))
        if let destination = overview.management?.destination {
            lines.append(closedManagementLine(destination))
        }
        return lines
    }

    private func sourceLines(_ overview: NativeMembershipOverview) -> [String] {
        overview.sources.map { source in
            joined(
                localized(membershipStatusLabel(source.status)),
                localized(membershipPlanText(source.plan)),
                localized(UiVerbatimText.protocolValue(source.status)),
                localized(UiVerbatimText.protocolValue(source.effectiveAt)),
                localized(UiVerbatimText.protocolValue(source.accessEndsAt)),
                source.renewal.map { localized(UiVerbatimText.protocolValue($0.effectiveAt)) } ?? ""
            )
        }
    }

    private func switchLines(_ overview: NativeMembershipOverview) -> [String] {
        overview.pending.switches.map { item in
            joined(
                localized(.nativeSwiftMembershipNativeBillingPending),
                localized(membershipPlanText(item.plan)),
                localized(UiVerbatimText.protocolValue(item.eligibleAt))
            )
        }
    }

    private func grantLines(_ overview: NativeMembershipOverview) -> [String] {
        guard overview.pending.grants > 0 else { return [] }
        return [
            joined(
                localized(.nativeSwiftMembershipMembershipGrants),
                localized(UiVerbatimText.protocolValue(String(overview.pending.grants)))
            )
        ]
    }

    private func operationLines(_ overview: NativeMembershipOverview) -> [String] {
        overview.pending.financialOperations.map { operation in
            let statusKey: UiMessageKey = operation.status == "pending"
                ? .nativeSwiftMembershipNativeBillingPending
                : .nativeSwiftMembershipNativeBillingUnavailable
            return joined(
                localized(statusKey),
                localized(UiVerbatimText.protocolValue(operation.kind)),
                localized(UiVerbatimText.protocolValue(operation.status))
            )
        }
    }

    private func verificationLines(_ overview: NativeMembershipOverview) -> [String] {
        overview.pending.verifications.map { verification in
            let pendingReason = verification.reasonCode == "purchase_pending"
            let statusKey: UiMessageKey = pendingReason || verification.status == "pending"
                ? .nativeSwiftMembershipNativeBillingPending
                : .nativeSwiftMembershipNativeBillingUnavailable
            let reason = verification.reasonCode.map { localized(UiVerbatimText.protocolValue($0)) } ?? ""
            return joined(localized(statusKey), localized(UiVerbatimText.protocolValue(verification.status)), reason)
        }
    }

    private func closedManagementLine(_ destination: String) -> String {
        joined(
            localized(.nativeSwiftMembershipNativeBillingUnavailable),
            localized(UiVerbatimText.protocolValue(destination))
        )
    }

    private func membershipStatusLabel(_ status: String) -> UiMessageKey {
        switch status {
        case "active":
            .nativeSwiftMembershipActive
        case "cancelled":
            .nativeSwiftMembershipCancelled
        case "expired":
            .nativeSwiftMembershipExpired
        case "past_due":
            .nativeSwiftMembershipPastDue
        case "paused":
            .nativeSwiftMembershipPaused
        default:
            .nativeSwiftMembershipNativeBillingUnavailable
        }
    }

    private func joined(_ parts: String...) -> String {
        parts.filter { !$0.isEmpty }.joined(separator: " ")
    }
}
