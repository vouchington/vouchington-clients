import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class LifecycleScenarioContractTests: NativeRouteSurfaceViewModelTestCase {
    func testContractRootRequiresExplicitEnvironment() {
        XCTAssertThrowsError(try FilamentsContractRoot.url(environment: [:])) { error in
            XCTAssertEqual(
                String(describing: error),
                "Missing required VOUCHA_FILAMENTS_CONTRACT_ROOT. Fetch Filaments contracts before running native contract tests."
            )
        }
    }

    func testContractRootRejectsInvalidExplicitDirectory() {
        XCTAssertThrowsError(
            try FilamentsContractRoot.url(environment: [
                FilamentsContractRoot.environmentKey: "/definitely-not-a-filaments-contract-root"
            ])
        ) { error in
            XCTAssertTrue(String(describing: error).contains("must name an existing directory"))
        }
    }

    func testSwiftClaimsMatchTheContract() async throws {
        let contract = try LifecycleScenarioContract.load()
        let scenarios = try contract.scenarios(claimedBy: "swift")
        XCTAssertEqual(scenarios.count, 21)

        for scenario in scenarios {
            let claim = try contract.claim(for: scenario, consumer: "swift")
            let observed = try await LifecycleScenarioSwiftAdapter.run(
                claim.adapter, input: scenario.input, client: makeClient
            )
            XCTAssertEqual(observed, scenario.expected, scenario.id)
        }
    }

    func testPaginationAdapterCoversEveryClaimedPaginationScenario() throws {
        let contract = try LifecycleScenarioContract.load()
        let pagination = try contract.scenarios(claimedBy: "swift").filter {
            $0.family.hasPrefix("forward-pagination")
        }
        XCTAssertEqual(pagination.map(\.id), [
            "forward-pagination-appends-deduplicates-and-advances-cursor",
            "forward-pagination-allows-one-request-in-flight",
            "forward-pagination-ignores-stale-success-after-reset",
            "forward-pagination-ignores-stale-failure-after-reset",
            "forward-pagination-failure-preserves-items-and-retries-cursor",
            "forward-pagination-removal-preserves-continuation",
            "forward-pagination-cancellation-preserves-items-and-cursor"
        ])
    }

    func testModerationAdapterRejectsMissingQueueRowAfterApproval() async throws {
        let input = LifecycleScenarioInput(
            preconditions: [
                "status": .string("pending"),
                "approvedAt": .null,
                "sentAt": .null,
                "viewerRole": .string("administrator")
            ],
            action: ["type": .string("approve")],
            serverOutcome: [
                "status": .string("dismissed"),
                "approvedAt": .null,
                "sentAt": .null,
                "latestLifecycleChangeChanged": .bool(true)
            ]
        )

        do {
            _ = try await LifecycleScenarioSwiftAdapter.run(
                "swift-moderation-appeals-view-model", input: input, client: makeClient
            )
            XCTFail("Expected production queue row requirement to fail")
        } catch {
            XCTAssertEqual(
                String(describing: error),
                "Production moderation view model removed appeal appeal-contract after approve"
            )
        }
    }
}
