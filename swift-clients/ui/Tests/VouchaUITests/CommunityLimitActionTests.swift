import Foundation
import VouchaCore
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityLimitActionTests: NativeRouteSurfaceViewModelTestCase {
    func testJoinAndPromotionConflictsShowGuidanceWithoutRetryOrReload() async throws {
        for action in ["join", "moderator", "owner", "transfer"] {
            let membership = action == "join"
            let path = "/api/v1/communities/builders/" + (membership ? "members" :
                action == "transfer" ? "ownership-transfers" : "members/user-2")
            for known in [true, false] {
                let before = CannedFeedURLProtocol.capturedURLs.count
                CannedFeedURLProtocol.handlers[path] = (conflict(membership: membership, known: known), 409)
                let model = try CommunityDetailViewModel(client: makeClient(), slug: "builders")
                switch action {
                case "join": await model.join()
                case "moderator": await model.updateMemberRole(userId: "user-2", role: .moderator)
                case "owner": await model.updateMemberRole(userId: "user-2", role: .owner)
                default: await model.transferOwnership(userId: "user-2")
                }
                XCTAssertEqual(model.state, .error(UiMessage(known ?
                        (membership ? .nativeCommunitiesMembershipLimitReached :
                            .nativeCommunitiesModeratorLimitReached) :
                        .nativeSwiftCommunityStatusActionFailed)))
                XCTAssertNil(model.statusMessage)
                XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count - before, 1)
                XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, path)
            }
        }
    }

    func testApplicationApprovalConflictKeepsPendingApplicationWithoutRetry() async throws {
        for known in [true, false] {
            let client = try makeClient()
            let model = CommunityDetailViewModel(
                client: client, slug: "builders", initialTab: .applications, isAdministrator: true
            )
            let applications = "/api/v1/communities/builders/applications"
            CannedFeedURLProtocol.handlers[applications] = (
                ApiFixtureLoader.data("web.communities.applications.default"),
                200
            )
            let page = try await model.loadApplicationRows(client: client, after: nil)
            model.rowPagination.reset(items: page.items)
            let id = try XCTUnwrap(page.items.first?.id)
            XCTAssertTrue(model.canReviewApplication(id))
            let before = CannedFeedURLProtocol.capturedURLs.count
            CannedFeedURLProtocol.handlers[applications + "/" + id] = (conflict(membership: true, known: known), 409)
            await model.approveApplication(applicationId: id)
            XCTAssertEqual(model.state, .error(UiMessage(known ? .nativeCommunitiesMembershipLimitReached :
                    .nativeSwiftCommunityStatusActionFailed)))
            XCTAssertEqual(model.applicationSummaries[id]?.status, "pending")
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count - before, 1)
        }
    }

    func testCreateAndInviteRedemptionConflictsDoNotRetry() async throws {
        for known in [true, false] {
            let client = try makeClient()
            let data = conflict(membership: true, known: known)
            CannedFeedURLProtocol.handlers["/api/v1/communities"] = (data, 409)
            CannedFeedURLProtocol.handlers["/api/v1/communities/invite-redemptions"] = (data, 409)
            let create = CommunityCreateViewModel(client: client)
            create.name = "Builders"
            create.turnstileToken = "verification"
            var before = CannedFeedURLProtocol.capturedURLs.count
            await create.create()
            XCTAssertEqual(create.state, .error(UiMessage(known ? .nativeCommunitiesMembershipLimitReached :
                    .nativeSwiftCommunityStatusUnableToCreateCommunity)))
            XCTAssertNil(create.createdCommunitySlug)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count - before, 1)
            let redemption = CommunityInviteRedemptionViewModel(client: client, code: "share-once")
            before = CannedFeedURLProtocol.capturedURLs.count
            await redemption.redeem()
            XCTAssertEqual(redemption.state, .error(UiMessage(known ? .nativeCommunitiesMembershipLimitReached :
                    .nativeSwiftCommunityStatusUnableToRedeemInvite)))
            XCTAssertNil(redemption.statusMessage)
            XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count - before, 1)
        }
    }

    func testCodeToCopyMappingPreservesBothErrorVariantsAndIgnoresUnexpectedCodes() {
        for (code, limit, key) in [
            (
                "COMMUNITY_MEMBERSHIP_LIMIT_REACHED",
                CommunityLimit.membership,
                UiMessageKey.nativeCommunitiesMembershipLimitReached
            ),
            (
                "COMMUNITY_MODERATOR_LIMIT_REACHED",
                CommunityLimit.moderator,
                UiMessageKey.nativeCommunitiesModeratorLimitReached
            )
        ] {
            for error in [
                VouchaError.api(statusCode: 409, preconditionCode: code),
                .apiMessage(statusCode: 409, preconditionCode: code, message: "Server text")
            ] {
                XCTAssertEqual(
                    communityLimitMessage(error, expected: limit, fallback: .nativeSwiftCommunityStatusActionFailed),
                    UiMessage(key)
                )
                XCTAssertEqual(
                    communityLimitMessage(error, expected: nil, fallback: .nativeSwiftCommunityStatusActionFailed),
                    UiMessage(.nativeSwiftCommunityStatusActionFailed)
                )
                XCTAssertEqual(
                    communityLimitMessage(
                        error,
                        expected: limit == .membership ? .moderator : .membership,
                        fallback: .nativeSwiftCommunityStatusActionFailed
                    ),
                    UiMessage(.nativeSwiftCommunityStatusActionFailed)
                )
            }
        }
    }

    private func conflict(membership: Bool, known: Bool) -> Data {
        if !known { return Data(#"{"code":"UNKNOWN_CONFLICT","message":"Conflict"}"#.utf8) }
        return ApiFixtureLoader.data(membership ? "native.communities.membership-cap.join.conflict" :
            "native.communities.moderator-cap.promotion.conflict")
    }
}
