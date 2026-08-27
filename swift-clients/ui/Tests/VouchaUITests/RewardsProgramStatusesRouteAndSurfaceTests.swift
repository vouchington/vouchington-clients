import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class RewardsProgramStatusesRouteAndSurfaceTests: XCTestCase {
    func testRewardsProgramStatusesHaveDedicatedAuthenticatedDestination() throws {
        let route = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/my/rewards-program-statuses")
        )
        XCTAssertEqual(route.entry.destinationIdentifier, .rewardsProgramStatuses)
        XCTAssertEqual(route.match.template, "/my/rewards-program-statuses")
        XCTAssertFalse(NativeRouteDestinationIdentifier.rewardsProgramStatuses.supportsRemoteNativeSurface)

        let profile = try XCTUnwrap(
            NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == .profileSettings }
        )
        XCTAssertFalse(profile.patterns.contains { $0.template == "/my/rewards-program-statuses" })

        let signedOut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false
        )
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try signedOut.inspect().find(button: "Search"))

        let restored = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true
        )
        XCTAssertNotEqual(signedOut.routeIdentity, restored.routeIdentity)
        XCTAssertTrue(restored.routeIdentity.contains("rewards-program-statuses"))
    }

    func testSurfaceUsesTopicNamesAndNativeDateControlsWithoutUUIDFallbacks() throws {
        let status = RewardsProgramStatus(
            id: "11111111-1111-7111-8111-111111111111",
            rewardsProgramStatusId: "22222222-2222-7222-8222-222222222222",
            since: LocalDate("2026-01-02"),
            until: LocalDate("2026-03-04"),
            rewardsProgramStatus: .init(
                id: "22222222-2222-7222-8222-222222222222",
                name: "Gold",
                slug: "gold"
            )
        )
        let viewModel = RewardsProgramStatusesViewModel(service: RewardsProgramStatusRouteServiceStub())
        viewModel.statuses = [status]
        let inspected = try RewardsProgramStatusesSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
            .inspect()

        XCTAssertNoThrow(try inspected.find(text: "Gold"))
        XCTAssertNoThrow(try inspected.find(text: "Since: Jan 2, 2026"))
        XCTAssertNoThrow(try inspected.find(text: "Until: Mar 4, 2026"))
        XCTAssertNoThrow(try inspected.find(button: "Edit"))
        XCTAssertNoThrow(try inspected.find(button: "Remove"))
        XCTAssertThrowsError(try inspected.find(text: status.id))
        XCTAssertThrowsError(try inspected.find(text: status.rewardsProgramStatusId))
    }

    func testDeletionRequiresNativeConfirmation() throws {
        let source = try repoSource(
            "swift-clients/ui/Sources/VouchaFeatures/RewardsProgramStatuses/RewardsProgramStatusRow.swift"
        )
        XCTAssertTrue(source.contains(".confirmationDialog("))
        XCTAssertTrue(source.contains("role: .destructive"))
        XCTAssertTrue(source.contains("role: .cancel"))
    }

    private func repoSource(_ relative: String, filePath: StaticString = #filePath) throws -> String {
        var root = URL(fileURLWithPath: "\(filePath)").deletingLastPathComponent()
        for _ in 0 ..< 16 {
            let candidate = root.appendingPathComponent(relative)
            if FileManager.default.fileExists(atPath: candidate.path) {
                return try String(contentsOf: candidate, encoding: .utf8)
            }
            root.deleteLastPathComponent()
        }
        throw XCTSkip("Could not find \(relative)")
    }
}

@MainActor
private final class RewardsProgramStatusRouteServiceStub: RewardsProgramStatusServicing {
    func statuses(after _: String?) async throws -> RewardsProgramStatusPage {
        .init(results: [])
    }

    func searchStatuses(query _: String) async throws -> [TopicSearchResult] {
        []
    }

    func create(body _: CreateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        throw RewardsProgramStatusRouteError.unexpected
    }

    func update(id _: String, body _: UpdateRewardsProgramStatusBody) async throws -> RewardsProgramStatus {
        throw RewardsProgramStatusRouteError.unexpected
    }

    func delete(id _: String) async throws {}
}

private enum RewardsProgramStatusRouteError: Error { case unexpected }
