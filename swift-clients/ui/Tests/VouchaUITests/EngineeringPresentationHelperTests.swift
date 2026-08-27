@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class EngineeringPresentationHelperTests: NativeRouteSurfaceViewModelTestCase {
    func testEngineeringHelpersCoverEveryPostgresqlJobAndValkeyFilterTitle() throws {
        let entry = try XCTUnwrap(
            AppSection.engineering.nativeParityGroups
                .flatMap(\.entries)
                .first { $0.destinationIdentifier == .engineeringPostgresql }
        )
        let surface = NativeEngineeringPostgresqlSurface(entry: entry, client: nil)

        XCTAssertEqual(
            EngineeringPsqlJobType.allCases.map { uiEnglish(surface.title(for: $0)) },
            ["Run Migrations", "Run Views", "Run Config-Driven", "Create Partitions", "Cleanup Partitions"]
        )
        XCTAssertEqual(
            EngineeringPsqlJobType.allCases.map { uiEnglish(surface.detail(for: $0)) },
            [
                "Apply pending migrations.",
                "Rebuild materialized views and derived views.",
                "Run config-driven schema operations.",
                "Create future monthly partitions.",
                "Drop expired monthly partitions."
            ]
        )
        XCTAssertEqual(
            EngineeringValkeyBloomFilterTarget.allCases.map { uiEnglish($0.uiMessage) },
            ["URL blocklist", "Email blocklist", "Embedding", "Entity cache", "API Keys"]
        )
    }

    func testNativeRouteFamilyGroupsHashByLocalizedTitleIdentity() throws {
        let group = try XCTUnwrap(AppSection.discover.nativeParityGroups.first)
        let duplicate = NativeRouteFamilyGroup(
            title: group.title,
            summary: group.summary,
            icon: "different",
            entries: [],
            requiredRoles: ["administrator"]
        )

        XCTAssertEqual(group, duplicate)
        XCTAssertEqual(Set([group, duplicate]).count, 1)
    }
}
