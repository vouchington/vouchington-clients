import Foundation
@testable import VouchaAPI
@testable import VouchaModels
import XCTest

final class ModerationReportPageFixtureTests: XCTestCase {
    func testClusteredReportPagesDecodeRecurringSidecarAndDistinctCursors() throws {
        let first = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.reports.clustered.default")
        )
        let second = try JSONDecoder.vouchaFixtureDecoder.decode(
            StaffClusteredModerationReportsResponse.self,
            from: ApiFixtureLoader.data("native.moderation.reports.clustered.page-2")
        )

        XCTAssertTrue(first.pageInfo.hasNextPage)
        XCTAssertEqual(first.pageInfo.hasPreviousPage, false)
        XCTAssertNotNil(first.pageInfo.endCursor)
        XCTAssertNotEqual(first.pageInfo.startCursor, first.pageInfo.endCursor)
        XCTAssertFalse(second.pageInfo.hasNextPage)
        XCTAssertEqual(second.pageInfo.hasPreviousPage, true)
        XCTAssertNotNil(second.pageInfo.startCursor)
        XCTAssertNotEqual(first.pageInfo.startCursor, second.pageInfo.startCursor)

        let firstSidecar = try XCTUnwrap(first.duplicateClusters.first)
        let secondSidecar = try XCTUnwrap(second.duplicateClusters.first)
        XCTAssertEqual(firstSidecar.id, secondSidecar.id)
        XCTAssertEqual(first.clusters.count, 4)
        XCTAssertEqual(firstSidecar.clusters.count, 3)
        XCTAssertEqual(second.clusters.count, 3)
        XCTAssertEqual(secondSidecar.clusters.count, 3)
        XCTAssertTrue(Set(firstSidecar.clusters.map(\.id)).isSubset(of: Set(first.clusters.map(\.id))))
        XCTAssertTrue(Set(secondSidecar.clusters.map(\.id)).isSubset(of: Set(second.clusters.map(\.id))))
        XCTAssertTrue(Set(first.clusters.map(\.id)).isDisjoint(with: second.clusters.map(\.id)))
        try assertSidecarMembersMatchResults(first)
        try assertSidecarMembersMatchResults(second)
    }

    private func assertSidecarMembersMatchResults(
        _ page: StaffClusteredModerationReportsResponse
    ) throws {
        let encoder = JSONEncoder()
        encoder.dateEncodingStrategy = .iso8601
        encoder.outputFormatting = [.sortedKeys]
        for member in page.duplicateClusters.flatMap(\.clusters) {
            let result = try XCTUnwrap(page.clusters.first { $0.id == member.id })
            XCTAssertEqual(try encoder.encode(member), try encoder.encode(result))
        }
    }
}
