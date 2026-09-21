import Foundation

actor CooldownSleepProbe {
    private(set) var delays: [UInt64] = []
    private var cancellationCount = 0
    private let suspends: Bool

    init(suspends: Bool = true) {
        self.suspends = suspends
    }

    func sleep(nanoseconds: UInt64) async throws {
        delays.append(nanoseconds)
        guard suspends else { return }
        do {
            try await Task.sleep(nanoseconds: 60_000_000_000)
        } catch {
            cancellationCount += 1
            throw error
        }
    }

    func waitForInvocationCount(_ expectedCount: Int) async -> Bool {
        let deadline = ContinuousClock.now + .seconds(2)
        while delays.count < expectedCount, ContinuousClock.now < deadline {
            await Task.yield()
        }
        return delays.count >= expectedCount
    }

    func waitForCancellationCount(_ expectedCount: Int) async -> Bool {
        let deadline = ContinuousClock.now + .seconds(2)
        while cancellationCount < expectedCount, ContinuousClock.now < deadline {
            await Task.yield()
        }
        return cancellationCount >= expectedCount
    }
}
