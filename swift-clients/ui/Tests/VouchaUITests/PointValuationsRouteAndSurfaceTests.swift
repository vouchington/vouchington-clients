import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class PointValuationsRouteAndSurfaceTests: XCTestCase {
    func testPointValuationsHasDedicatedAuthenticatedDestination() throws {
        let route = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/my/rewards-program-point-valuations")
        )
        XCTAssertEqual(route.entry.destinationIdentifier, .pointValuations)
        XCTAssertEqual(route.match.template, "/my/rewards-program-point-valuations")
        XCTAssertFalse(NativeRouteDestinationIdentifier.pointValuations.supportsRemoteNativeSurface)

        let profile = try XCTUnwrap(
            NativeRouteCatalog.includedEntries.first { $0.destinationIdentifier == .profileSettings }
        )
        XCTAssertFalse(
            profile.patterns.contains { $0.template == "/my/rewards-program-point-valuations" }
        )
        let signedOut = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: false
        )
        XCTAssertNoThrow(try signedOut.inspect().find(text: "Sign in required"))
        XCTAssertThrowsError(try signedOut.inspect().find(text: "Add a point valuation"))

        let restored = NativeRouteDestinationView(
            entry: route.entry,
            routeMatch: route.match,
            isSignedIn: true
        )
        XCTAssertNotEqual(signedOut.routeIdentity, restored.routeIdentity)
        XCTAssertTrue(restored.routeIdentity.contains("point-valuations"))
    }

    func testSurfaceUsesNativeControlsAndNeverRendersUUIDFallbacks() throws {
        let valuation = try PointValuation(
            id: "11111111-1111-7111-8111-111111111111",
            rewardsProgramId: "22222222-2222-7222-8222-222222222222",
            valuePerPoint: ScaledMoney(amount: 1_500_000, currency: "usd"),
            note: "Everyday redemption",
            rewardsProgram: .init(
                id: "22222222-2222-7222-8222-222222222222",
                name: "Example Rewards",
                slug: "example-rewards"
            )
        )
        let viewModel = PointValuationsViewModel(service: PointValuationRouteServiceStub())
        viewModel.valuations = [valuation]
        let inspected = try PointValuationsSurface(viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
            .inspect()

        XCTAssertNoThrow(try inspected.find(text: "Point Valuations"))
        XCTAssertNoThrow(try inspected.find(text: "Example Rewards"))
        XCTAssertNoThrow(try inspected.find(text: "$1.5 per point"))
        XCTAssertNoThrow(try inspected.find(text: "Everyday redemption"))
        XCTAssertNoThrow(try inspected.find(button: "Edit"))
        XCTAssertNoThrow(try inspected.find(button: "Remove"))
        XCTAssertThrowsError(try inspected.find(text: valuation.id))
        XCTAssertThrowsError(try inspected.find(text: valuation.rewardsProgramId))
    }

    func testDeletionRequiresConfirmation() throws {
        let source = try repoSource(
            "swift-clients/ui/Sources/VouchaFeatures/PointValuations/PointValuationRow.swift"
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
private final class PointValuationRouteServiceStub: PointValuationServicing {
    func valuations(after _: String?) async throws -> PointValuationPage {
        .init(results: [])
    }

    func searchRewardsPrograms(query _: String) async throws -> [TopicSearchResult] {
        []
    }

    func create(body _: CreatePointValuationBody) async throws -> PointValuation {
        throw PointValuationRouteError.unexpected
    }

    func update(id _: String, body _: UpdatePointValuationBody) async throws -> PointValuation {
        throw PointValuationRouteError.unexpected
    }

    func delete(id _: String) async throws {}
}

private enum PointValuationRouteError: Error {
    case unexpected
}
