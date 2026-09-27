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
        var selection = try makeSelection(isAdministrator: true)
        XCTAssertFalse(selection.setSelected("oauth:read", selected: true))
        XCTAssertFalse(selection.setSelected("feed:read", selected: true))
        XCTAssertTrue(selection.setSelected("data:read", selected: true))
        XCTAssertFalse(selection.setSelected("admin:read", selected: true))
        selection.configure(type: .rss, isAdministrator: true)
        XCTAssertTrue(selection.permissions.isEmpty)
        XCTAssertEqual(selection.availableScopes.map(\.scope), ["feed:read"])
        XCTAssertTrue(selection.setSelected("feed:read", selected: true))
        XCTAssertTrue(selection.isValid)
        selection.configure(type: .mcp, isAdministrator: false)
        XCTAssertFalse(selection.availableScopes.contains { $0.audience == .admin })
    }

    func testMissingCrossAudienceAndCyclicPrerequisitesFailClosed() {
        for scopes in [
            [scope("write", requires: "missing")],
            [scope("a", requires: "b"), scope("b", requires: "a")],
            [scope("user", requires: "admin"), scope("admin", audience: .admin)],
            [scope("duplicate"), scope("duplicate")]
        ] {
            let selection = ApiKeyScopeSelection(scopes: scopes, type: .mcp, isAdministrator: true)
            XCTAssertTrue(selection.availableScopes.isEmpty)
            XCTAssertFalse(selection.isValid)
        }
    }

    func testCatalogRefreshPreservesOnlyValidSelectionAndClearKeepsOtherKeyTypesAvailable() throws {
        var selection = try makeSelection(isAdministrator: true)
        XCTAssertTrue(selection.setSelected("admin:read", selected: true))
        XCTAssertTrue(selection.isValid)
        selection.replaceCatalog([scope("admin:read", audience: .admin), scope("feed:read", audience: .api)])
        XCTAssertEqual(selection.permissions, ["admin:read"])
        selection.clear()
        XCTAssertFalse(selection.isValid)
        selection.configure(type: .rss, isAdministrator: true)
        XCTAssertTrue(selection.setSelected("feed:read", selected: true))
        selection.replaceCatalog([scope("new-feed:read", audience: .api)])
        XCTAssertTrue(selection.permissions.isEmpty)
        XCTAssertEqual(selection.availableScopes.map(\.scope), ["new-feed:read"])
    }

    private func makeSelection(isAdministrator: Bool = false) throws -> ApiKeyScopeSelection {
        ApiKeyScopeSelection(scopes: [
            scope("data:read"), scope("data:write", requires: "data:read"),
            scope("private:write", requires: "data:write"), scope("umbrella:read"),
            scope("admin:read", audience: .admin), scope("feed:read", audience: .api),
            scope("oauth:read", surfaces: [.oauth])
        ], type: .mcp, isAdministrator: isAdministrator)
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
