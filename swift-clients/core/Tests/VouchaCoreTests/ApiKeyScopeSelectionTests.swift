import Foundation
import VouchaModels
import XCTest

final class ApiKeyScopeSelectionTests: XCTestCase {
    func testSelectionAddsTransitivePrerequisitesAndClearingRemovesDependents() throws {
        var selection = try makeSelection()
        XCTAssertTrue(selection.setSelected("private:write", selected: true))
        XCTAssertEqual(selection.permissions, ["data:read", "data:write", "private:write"])
        XCTAssertTrue(selection.setSelected("data:read", selected: false))
        XCTAssertTrue(selection.permissions.isEmpty)
    }

    func testUmbrellaSelectionDoesNotSelectExplicitResourceScopes() throws {
        var selection = try makeSelection()
        XCTAssertTrue(selection.setSelected("umbrella:read", selected: true))
        XCTAssertEqual(selection.permissions, ["umbrella:read"])
        XCTAssertFalse(selection.permissions.contains("private:write"))
        XCTAssertFalse(selection.setSelected("unknown:*", selected: true))
    }

    func testSurfaceAudienceAndTypeLimitSelectionWithoutMixingAudiences() throws {
        var selection = try makeSelection()
        XCTAssertFalse(selection.setSelected("oauth:read", selected: true))
        XCTAssertFalse(selection.setSelected("feed:read", selected: true))
        XCTAssertTrue(selection.setSelected("data:read", selected: true))
        XCTAssertFalse(selection.setSelected("admin:read", selected: true))
        selection.configure(type: .rss)
        XCTAssertTrue(selection.permissions.isEmpty)
        XCTAssertEqual(selection.availableScopes.map(\.scope), ["feed:read"])
        XCTAssertTrue(selection.setSelected("feed:read", selected: true))
        XCTAssertTrue(selection.isValid)
        selection.configure(type: .mcp)
        XCTAssertFalse(selection.availableScopes.contains { $0.audience == .admin })
    }

    func testAdministratorCannotSelectAdminMcpScopesFromIncorrectCatalogueMetadata() {
        var selection = ApiKeyScopeSelection(scopes: [
            scope("mcp.user:read"),
            scope("mcp.admin:read", audience: .admin),
            scope("mcp.admin:write", audience: .user)
        ], type: .mcp)

        XCTAssertEqual(selection.availableScopes.map(\.scope), ["mcp.user:read"])
        XCTAssertFalse(selection.setSelected("mcp.admin:read", selected: true))
        XCTAssertFalse(selection.setSelected("mcp.admin:write", selected: true))
        XCTAssertTrue(selection.permissions.isEmpty)
        XCTAssertTrue(selection.setSelected("mcp.user:read", selected: true))
        XCTAssertEqual(selection.permissions, ["mcp.user:read"])
    }

    func testMissingCrossAudienceAndCyclicPrerequisitesFailClosed() {
        for scopes in [
            [scope("write", requires: "missing")],
            [scope("a", requires: "b"), scope("b", requires: "a")],
            [scope("user", requires: "admin"), scope("admin", audience: .admin)],
            [scope("duplicate"), scope("duplicate")]
        ] {
            let selection = ApiKeyScopeSelection(scopes: scopes, type: .mcp)
            XCTAssertTrue(selection.availableScopes.isEmpty)
            XCTAssertFalse(selection.isValid)
        }
    }

    func testCatalogRefreshPreservesOnlyValidSelectionAndClearKeepsOtherKeyTypesAvailable() throws {
        var selection = try makeSelection()
        XCTAssertTrue(selection.setSelected("data:read", selected: true))
        XCTAssertTrue(selection.isValid)
        selection.replaceCatalog([scope("data:read"), scope("feed:read", audience: .api)])
        XCTAssertEqual(selection.permissions, ["data:read"])
        selection.clear()
        XCTAssertFalse(selection.isValid)
        selection.configure(type: .rss)
        XCTAssertTrue(selection.setSelected("feed:read", selected: true))
        selection.replaceCatalog([scope("new-feed:read", audience: .api)])
        XCTAssertTrue(selection.permissions.isEmpty)
        XCTAssertEqual(selection.availableScopes.map(\.scope), ["new-feed:read"])
    }

    func testMemberAudiencesRemainDistinctAndUnsupportedAudiencesAreExcluded() throws {
        let audiences = ["user", "community_moderation", "brand", "legal", "admin", "api"]
        let scopes = try audiences.map { audience in
            try JSONDecoder().decode(CredentialScope.self, from: Data("""
            {"scope":"\(audience):read","resource":"fixture","action":"read",
             "audience":"\(audience)","surfaces":["api-key"],"requires":null,"descriptionKey":null}
            """.utf8))
        }
        var selection = ApiKeyScopeSelection(scopes: scopes, type: .mcp)
        XCTAssertEqual(selection.availableScopes.map(\.scope), ["brand:read", "community_moderation:read", "user:read"])
        for audience in ["user", "community_moderation", "brand"] {
            selection.clear()
            XCTAssertTrue(selection.setSelected("\(audience):read", selected: true))
            XCTAssertTrue(selection.isValid)
            for other in audiences where other != audience {
                XCTAssertFalse(selection.setSelected("\(other):read", selected: true))
            }
            XCTAssertEqual(selection.permissions, ["\(audience):read"])
        }
        XCTAssertThrowsError(try JSONDecoder().decode(ScopeAudience.self, from: Data("\"future\"".utf8)))
    }

    private func makeSelection() throws -> ApiKeyScopeSelection {
        ApiKeyScopeSelection(scopes: [
            scope("data:read"), scope("data:write", requires: "data:read"),
            scope("private:write", requires: "data:write"), scope("umbrella:read"),
            scope("admin:read", audience: .admin), scope("feed:read", audience: .api),
            scope("oauth:read", surfaces: [.oauth])
        ], type: .mcp)
    }

    private func scope(
        _ value: String,
        audience: ScopeAudience = .user,
        surfaces: [ScopeSurface] = [.apiKey],
        requires: String? = nil
    ) -> CredentialScope {
        CredentialScope(
            scope: value,
            resource: value,
            action: .read,
            audience: audience,
            requires: requires,
            descriptionKey: nil,
            surfaces: surfaces
        )
    }
}
