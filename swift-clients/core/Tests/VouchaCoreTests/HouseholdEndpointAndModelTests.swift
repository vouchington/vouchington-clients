import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class HouseholdEndpointAndModelTests: XCTestCase {
    func testSharedFixturesDecodeHouseholdListVariantsAndNullableMembershipFields() throws {
        let decoder = makeVouchaDecoder()
        let empty = try decoder.decode(HouseholdPage.self, from: ApiFixtureLoader.data("native.households.empty"))
        let single = try decoder.decode(
            HouseholdPage.self,
            from: ApiFixtureLoader.data("native.households.single-owned")
        )
        let multiple = try decoder.decode(
            HouseholdPage.self,
            from: ApiFixtureLoader.data("native.households.multiple")
        )
        let memberships = try decoder.decode(
            HouseholdMembershipPage.self,
            from: ApiFixtureLoader.data("native.household-memberships.multiple")
        )
        let created = try decoder.decode(
            HouseholdCreateResponse.self,
            from: ApiFixtureLoader.data("native.households.create.default")
        )

        XCTAssertTrue(empty.results.isEmpty)
        XCTAssertEqual(single.results.count, 1)
        XCTAssertNil(single.results[0].createdAt)
        XCTAssertEqual(multiple.results.map(\.id), [householdId, "00000000-0000-7000-8000-000000000102"])
        XCTAssertEqual(memberships.results[0].individual.username, "household-alice")
        XCTAssertNil(memberships.results[1].individual.userId)
        XCTAssertNil(memberships.results[2].relationship)
        XCTAssertNotNil(created.household.createdAt)
    }

    func testHouseholdEndpointsMatchManifestContractsAndEncodeEmptyCreateBody() {
        let owned = Endpoint.households(access: .owned, limit: 1)
        XCTAssertEqual(owned.queryItems, [
            URLQueryItem(name: "access", value: "owned"),
            URLQueryItem(name: "limit", value: "1")
        ])
        let memberPage = Endpoint.households(access: .member, after: "opaque", limit: 25)
        XCTAssertEqual(memberPage.queryItems, [
            URLQueryItem(name: "access", value: "member"),
            URLQueryItem(name: "limit", value: "25"),
            URLQueryItem(name: "after", value: "opaque")
        ])
        assertEndpoint(.createHousehold, method: .POST, path: "/api/v1/households", body: [:])
        let memberships = Endpoint.householdMemberships(householdId: householdId, after: "next", limit: 1)
        assertEndpoint(memberships, path: "/api/v1/households/\(householdId)/memberships")
        XCTAssertEqual(memberships.queryItems, [
            URLQueryItem(name: "limit", value: "1"),
            URLQueryItem(name: "after", value: "next")
        ])
        assertEndpoint(
            .deleteHouseholdMembership(householdId: householdId, membershipId: membershipId),
            method: .DELETE,
            path: "/api/v1/households/\(householdId)/memberships/\(membershipId)"
        )
    }

    func testPaginationFixturesDecodeExactCursorsAndAccessPages() throws {
        let decoder = makeVouchaDecoder()
        let owned = try decoder.decode(HouseholdPage.self, from: ApiFixtureLoader.data("native.households.owned"))
        let members = try decoder.decode(
            HouseholdPage.self,
            from: ApiFixtureLoader.data("native.households.member.default")
        )
        let memberPageTwo = try decoder.decode(
            HouseholdPage.self,
            from: ApiFixtureLoader.data("native.households.member.page-2")
        )
        let memberships = try decoder.decode(
            HouseholdMembershipPage.self,
            from: ApiFixtureLoader.data("native.household-memberships.page-1")
        )

        XCTAssertEqual(owned.results.map(\.ownerId), ["00000000-0000-7000-8000-000000000001"])
        XCTAssertFalse(owned.pageInfo.hasNextPage)
        XCTAssertEqual(members.results.map(\.id), ["00000000-0000-7000-8000-000000000102"])
        XCTAssertTrue(members.pageInfo.hasNextPage)
        XCTAssertNotNil(members.pageInfo.endCursor)
        XCTAssertEqual(memberPageTwo.results.map(\.id), ["00000000-0000-7000-8000-000000000103"])
        XCTAssertFalse(memberPageTwo.pageInfo.hasNextPage)
        XCTAssertTrue(memberships.pageInfo.hasNextPage)
        XCTAssertNotNil(memberships.pageInfo.endCursor)
    }

    func testHouseholdEndpointIdentifiersAreEscapedAsSinglePathSegments() {
        XCTAssertEqual(
            Endpoint.householdMemberships(householdId: "home/primary:owner").path,
            "/api/v1/households/home%2Fprimary%3Aowner/memberships"
        )
        XCTAssertEqual(
            Endpoint.deleteHouseholdMembership(householdId: "home/one", membershipId: "member/two").path,
            "/api/v1/households/home%2Fone/memberships/member%2Ftwo"
        )
    }

    private let householdId = "00000000-0000-7000-8000-000000000101"
    private let membershipId = "00000000-0000-7000-8000-000000000201"
}
