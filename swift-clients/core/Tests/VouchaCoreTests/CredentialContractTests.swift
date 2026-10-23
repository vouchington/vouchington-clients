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

    func testStagedFinancialScopesNeedExactSelectionAndTheirOwnReadPrerequisites() throws {
        let catalog = try makeVouchaDecoder().decode(
            ScopeCatalogResponse.self,
            from: ApiFixtureLoader.data("shared.scopes.catalog")
        )
        let financialScopes = [
            "financial-profile:read", "financial-profile:write", "spending:read", "spending:write"
        ]
        XCTAssertEqual(
            Set(catalog.scopes.filter { financialScopes.contains($0.scope) }.map(\.scope)),
            Set(financialScopes)
        )
        XCTAssertEqual(
            catalog.scopes.first { $0.scope == "financial-profile:write" }?.requires,
            "financial-profile:read"
        )
        XCTAssertEqual(catalog.scopes.first { $0.scope == "spending:write" }?.requires, "spending:read")

        var selection = ApiKeyScopeSelection(scopes: catalog.scopes, type: .mcp)
        XCTAssertTrue(selection.setSelected("mcp.user:read", selected: true))
        XCTAssertEqual(selection.permissions, ["mcp.user:read"])
        XCTAssertTrue(selection.setSelected("financial-profile:write", selected: true))
        XCTAssertEqual(
            selection.permissions,
            ["financial-profile:read", "financial-profile:write", "mcp.user:read"]
        )
        XCTAssertTrue(selection.setSelected("financial-profile:read", selected: false))
        XCTAssertEqual(selection.permissions, ["mcp.user:read"])
        XCTAssertTrue(selection.setSelected("spending:write", selected: true))
        XCTAssertEqual(selection.permissions, ["mcp.user:read", "spending:read", "spending:write"])
    }

    func testStagedCommunityModerationScopesPreserveDescriptionsAndReadPrerequisite() throws {
        let catalog = try makeVouchaDecoder().decode(
            ScopeCatalogResponse.self,
            from: ApiFixtureLoader.data("shared.scopes.catalog")
        )
        let read = try XCTUnwrap(catalog.scopes.first { $0.scope == "community-moderation:read" })
        let write = try XCTUnwrap(catalog.scopes.first { $0.scope == "community-moderation:write" })
        XCTAssertEqual(read.descriptionKey, .communityModerationRead)
        XCTAssertEqual(write.descriptionKey, .communityModerationWrite)
        XCTAssertNil(read.requires)
        XCTAssertEqual(write.requires, read.scope)
        XCTAssertEqual(Set(write.surfaces), Set([.apiKey, .oauth]))

        var selection = ApiKeyScopeSelection(scopes: catalog.scopes, type: .mcp)
        XCTAssertTrue(selection.setSelected("mcp.user:read", selected: true))
        XCTAssertEqual(selection.permissions, ["mcp.user:read"])
        XCTAssertFalse(selection.setSelected(write.scope, selected: true))
        XCTAssertEqual(selection.permissions, ["mcp.user:read"])
        selection.clear()
        XCTAssertTrue(selection.setSelected(write.scope, selected: true))
        XCTAssertEqual(selection.permissions, [read.scope, write.scope].sorted())
        XCTAssertTrue(selection.setSelected(read.scope, selected: false))
        XCTAssertTrue(selection.permissions.isEmpty)
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

    func testDescriptionMetadataPreservesKnownKeysAndIgnoresUnknownKeysWithoutDroppingFields() throws {
        let descriptions: [(String, ScopeDescriptionKey?)] = [
            ("future_description", nil),
            ("mcp_user_full_access", .mcpUserFullAccess),
            ("mcp_admin_full_access", .mcpAdminFullAccess),
            ("financial_profile_read", .financialProfileRead),
            ("financial_profile_write", .financialProfileWrite),
            ("spending_read", .spendingRead),
            ("spending_write", .spendingWrite),
            ("community_moderation_read", .communityModerationRead),
            ("community_moderation_write", .communityModerationWrite)
        ]
        for (raw, expected) in descriptions {
            var fields: [String: Any] = [
                "scope": "x:write", "resource": "x", "action": "write", "audience": "user",
                "requires": "x:read", "description_key": raw, "surfaces": ["api-key", "oauth"]
            ]
            let scope = try makeVouchaDecoder().decode(
                CredentialScope.self, from: JSONSerialization.data(withJSONObject: fields)
            )
            XCTAssertEqual(scope.descriptionKey, expected)
            let encoder = JSONEncoder()
            encoder.keyEncodingStrategy = .convertToSnakeCase
            let reencoded = try JSONSerialization.jsonObject(with: encoder.encode(scope)) as? NSDictionary
            if expected == nil { fields["description_key"] = NSNull() }
            XCTAssertEqual(reencoded, fields as NSDictionary)
        }
    }

    func testMissingAndNullDescriptionMetadataDecodeAsAbsent() throws {
        for description in ["", #","description_key":null"#] {
            let data = Data(
                "{\"scope\":\"x:read\",\"resource\":\"x\",\"action\":\"read\",\"audience\":\"user\",\"surfaces\":[\"api-key\"]\(description)}"
                    .utf8
            )
            let scope = try makeVouchaDecoder().decode(CredentialScope.self, from: data)
            XCTAssertNil(scope.descriptionKey)
            XCTAssertEqual(scope.scope, "x:read")
        }
    }

    func testNonStringDescriptionMetadataStillFailsDecoding() {
        let data = Data(
            #"{"scope":"x:read","resource":"x","action":"read","audience":"user","description_key":7,"surfaces":["api-key"]}"#
                .utf8
        )
        XCTAssertThrowsError(try makeVouchaDecoder().decode(CredentialScope.self, from: data))
    }

    func testStagedMessageScopeDescriptionsRemainOptionalPresentationMetadata() throws {
        let catalog = try makeVouchaDecoder().decode(
            ScopeCatalogResponse.self, from: ApiFixtureLoader.data("shared.scopes.catalog")
        )
        for name in ["messages:read", "messages:write"] {
            let scope = try XCTUnwrap(catalog.scopes.first { $0.scope == name })
            XCTAssertNil(scope.descriptionKey)
            XCTAssertEqual(scope.resource, "messages")
            XCTAssertEqual(Set(scope.surfaces), Set([.apiKey, .oauth]))
        }
        XCTAssertEqual(
            catalog.scopes.first { $0.scope == "financial-profile:read" }?.descriptionKey,
            .financialProfileRead
        )
        XCTAssertEqual(catalog.scopes.first { $0.scope == "mcp.user:read" }?.descriptionKey, .mcpUserFullAccess)
    }

    func testGrantEndpointEscapesOwnedIdAndForwardsOpaqueCursor() {
        let endpoint = Endpoint.revokeMyOAuthGrant(id: "grant/other")
        XCTAssertEqual(endpoint.method, .DELETE)
        XCTAssertTrue(endpoint.path.contains("grant%2Fother"))
        let list = Endpoint.myOAuthGrants(after: "opaque+/=cursor")
        XCTAssertEqual(list.queryItems.first { $0.name == "after" }?.value, "opaque+/=cursor")
    }

    func testAudienceCatalogKeepsApiKeyAndAdministratorFlags() {
        let audience = ScopeCatalogAudience(
            audience: "user", acceptsApiKeys: true, requiresAdministrator: false
        )
        let catalog = ScopeCatalogResponse(audiences: [audience], scopes: [])
        XCTAssertEqual(catalog.audiences.first?.audience, "user")
        XCTAssertEqual(catalog.audiences.first?.acceptsApiKeys, true)
        XCTAssertEqual(catalog.audiences.first?.requiresAdministrator, false)
        XCTAssertTrue(catalog.scopes.isEmpty)
    }
}
