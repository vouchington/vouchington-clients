import Foundation
@testable import VouchaAPI
import VouchaModels
import XCTest

final class CredentialContractTests: XCTestCase {
    func testCredentialFixtureRoundTripsAndRegisteredEndpointsMatchStagedRequests() throws {
        for fixture in credentialFixtureCoverage {
            try fixture.run(fixture.id)
        }
        for (id, endpoint) in ApiFixtureEndpointCoverageTests.credentialFixtureEndpoints {
            let fixture = try XCTUnwrap(ApiFixtureLoader.swiftRouteFixtures.first { $0.id == id })
            XCTAssertEqual(endpoint.method.rawValue, fixture.method)
            XCTAssertEqual(endpoint.path, fixture.path)
            XCTAssertEqual(try requestBodyJSON(from: endpoint), fixture.requestBody)
            XCTAssertEqual(
                Dictionary(uniqueKeysWithValues: endpoint.queryItems.map { ($0.name, $0.value ?? "") }),
                fixture.query
            )
        }
    }

    func testStagedCatalogDrivesSelectionAndCreationFixtureDecodes() throws {
        let decoder = makeVouchaDecoder()
        let catalog = try decoder.decode(
            ScopeCatalogResponse.self,
            from: ApiFixtureLoader.data("shared.scopes.catalog")
        )
        var selection = ApiKeyScopeSelection(scopes: catalog.scopes, type: .mcp)
        let write = try XCTUnwrap(catalog.scopes
            .first { $0.descriptionKey == .mcpUserFullAccess && $0.action == .write })
        XCTAssertTrue(selection.setSelected(write.scope, selected: true))
        XCTAssertEqual(selection.permissions, try [XCTUnwrap(write.requires), write.scope].sorted())
        XCTAssertFalse(selection.availableScopes.contains { $0.audience == .admin })
        let response = try decoder.decode(
            ApiKeyCreationResponse.self,
            from: ApiFixtureLoader.data("native.my.api-keys.create")
        )
        XCTAssertEqual(response.apiKey.permissions, selection.permissions)
        XCTAssertEqual(response.rawKey, "fixture-raw-api-key")
        XCTAssertEqual(response.apiKey.type, .mcp)
    }

    func testStagedGrantsPreserveClientScopeAndActivityMetadata() throws {
        let grants = try makeVouchaDecoder().decode(
            Page<OAuthGrant>.self,
            from: ApiFixtureLoader.data("native.my.oauth-grants.paginated")
        )
        let grant = try XCTUnwrap(grants.results.first)
        XCTAssertEqual(grant.client.clientName, "Fixture Agent")
        XCTAssertTrue(grant.client.verified)
        XCTAssertEqual(grant.resource, "https://voucha.ai/api/v1/mcp")
        XCTAssertEqual(grant.scopes, ["mcp.user:read", "mcp.user:write"])
        XCTAssertNil(grant.lastUsedAt)
        XCTAssertTrue(grants.pageInfo.hasNextPage)
        XCTAssertEqual(grants.pageInfo.endCursor, "fixture-oauth-grant-end-cursor")
    }

    func testUnknownPresentationMetadataFailsDecoding() {
        let data = Data(
            #"{"scopes":[{"scope":"x:read","resource":"x","action":"read","audience":"user","requires":null,"description_key":"unknown-description","surfaces":["api-key"]}]}"#
                .utf8
        )
        XCTAssertThrowsError(try makeVouchaDecoder().decode(ScopeCatalogResponse.self, from: data))
    }

    func testGrantEndpointEscapesOwnedIdAndForwardsOpaqueCursor() {
        let endpoint = Endpoint.revokeMyOAuthGrant(id: "grant/other")
        XCTAssertEqual(endpoint.method, .DELETE)
        XCTAssertTrue(endpoint.path.contains("grant%2Fother"))
        let list = Endpoint.myOAuthGrants(after: "opaque+/=cursor")
        XCTAssertEqual(list.queryItems.first { $0.name == "after" }?.value, "opaque+/=cursor")
    }
}
