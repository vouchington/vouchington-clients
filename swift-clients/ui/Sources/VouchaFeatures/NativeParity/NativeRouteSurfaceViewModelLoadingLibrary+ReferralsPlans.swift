import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    func loadReferralRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        try await loadReferralPage(client: client, after: nil).rows.map(\.row)
    }

    func loadReferralPage(client: APIClient, after: String?) async throws -> NativeForwardPage {
        if routeMatch?.path == "/my/referral-links" {
            let response: ReferralLinkFeedResponse = try await client.send(.referralLinks(after: after, limit: 25))
            let rows = response.results.map {
                forwardRow(
                    id: $0.id,
                    icon: "link",
                    title: rawText($0.label ?? $0.url ?? $0.id),
                    detail: $0.userId.map(rawText) ?? appText(.nativeSwiftRouteSurfaceReferralLink)
                )
            }
            return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
        }
        let response: ReferralClickLogResponse = try await client.send(.myReferralClicks(after: after, limit: 25))
        let rows = response.results.compactMap { result -> NativeForwardRow? in
            guard let click = response.clicks[result.id] else { return nil }
            return forwardRow(
                id: result.id,
                icon: "chart.line.uptrend.xyaxis",
                title: rawText(click.landingUrl),
                detail: appText(
                    click.signedUpAt == nil
                        ? .nativeSwiftRouteSurfaceClick
                        : .nativeSwiftRouteSurfaceSignup
                )
            )
        }
        return NativeForwardPage(rows: rows, pageInfo: response.pageInfo)
    }

    func loadPlanRows(client: APIClient) async throws -> [NativeRouteDestinationRow] {
        let response: MembershipPlansResponse = try await client.send(.membershipPlans)
        return response.plans.flatMap { plan, skus in
            skus.map {
                let interval: UiVerbatimText = switch $0.interval {
                case "monthly": appText(.nativeSwiftMembershipMonth)
                case "yearly": appText(.nativeSwiftMembershipYear)
                default: rawText($0.interval)
                }
                return row(
                    "creditcard",
                    rawText(plan),
                    appText(
                        .nativeSwiftMembershipPerPrice,
                        textParameters: ["interval": interval],
                        currencyParameters: [
                            "price": UiMessageCurrencyParameter(
                                $0.price.knownCurrencyMajorUnitDecimal ?? 0,
                                code: $0.price.currency.uppercased()
                            )
                        ]
                    )
                )
            }
        }
    }
}
