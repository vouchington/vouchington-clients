import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class EngineeringQueuesDelayedCountsTests: NativeRouteSurfaceViewModelTestCase {
    func testQueueSummaryRendersDelayedBesideWaitingAtBothLevels() throws {
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        viewModel.state = .loaded
        viewModel.stats = EngineeringAggregatedQueueStats(
            totalWaiting: 0,
            totalActive: 2,
            totalCompleted: 3,
            totalFailed: 4,
            totalDelayed: 9,
            queueCount: 1
        )
        viewModel.queues = [EngineeringQueueStats(
            name: "priority",
            waiting: 0,
            active: 2,
            completed: 3,
            failed: 4,
            delayed: 7,
            paused: false
        )]

        let surface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        let rendered = try surface.loadedContent(viewModel).inspect()

        XCTAssertEqual(
            try rendered.find(text: "0 waiting, 9 delayed, 2 active, 4 failed, 1 queues").string(),
            "0 waiting, 9 delayed, 2 active, 4 failed, 1 queues"
        )
        XCTAssertEqual(
            try rendered.find(text: "0 waiting · 7 delayed · 2 active · 4 failed · 3 completed").string(),
            "0 waiting · 7 delayed · 2 active · 4 failed · 3 completed"
        )
    }

    func testQueueSummaryKeepsZeroDelayedVisible() throws {
        let viewModel = try NativeEngineeringQueuesViewModel(client: makeClient())
        viewModel.state = .loaded
        viewModel.stats = EngineeringAggregatedQueueStats(
            totalWaiting: 3,
            totalActive: 0,
            totalCompleted: 0,
            totalFailed: 0,
            totalDelayed: 0,
            queueCount: 1
        )
        viewModel.queues = [EngineeringQueueStats(
            name: "ordinary",
            waiting: 3,
            active: 0,
            completed: 0,
            failed: 0,
            delayed: 0,
            paused: false
        )]

        let surface = try NativeEngineeringQueuesSurface(entry: entry(for: .engineeringQueues), client: nil)
        let rendered = try surface.loadedContent(viewModel).inspect()

        XCTAssertEqual(
            try rendered.find(text: "3 waiting, 0 delayed, 0 active, 0 failed, 1 queues").string(),
            "3 waiting, 0 delayed, 0 active, 0 failed, 1 queues"
        )
        XCTAssertEqual(
            try rendered.find(text: "3 waiting · 0 delayed · 0 active · 0 failed · 0 completed").string(),
            "3 waiting · 0 delayed · 0 active · 0 failed · 0 completed"
        )
    }
}
