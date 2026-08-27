import Observation
import VouchaAPI
import VouchaCore

@Observable
@MainActor
final class GrowthDashboardViewModel {
    private(set) var metrics: GrowthMetrics?
    private(set) var state: LoadState = .idle
    var range: GrowthRange

    private let client: APIClient?

    init(
        client: APIClient?,
        initialRange: GrowthRange = .thirtyDays,
        initialMetrics: GrowthMetrics? = nil
    ) {
        self.client = client
        range = initialRange
        metrics = initialMetrics
        if initialMetrics != nil {
            state = .loaded
        }
    }

    var isLoading: Bool {
        if case .loading = state {
            return true
        }
        return false
    }

    func load() async {
        guard let client else {
            state = .error(.api(statusCode: 401, preconditionCode: nil))
            return
        }

        let expectedRange = range
        state = .loading
        do {
            let fetchedMetrics: GrowthMetrics = try await client.send(.growthMetrics(range: expectedRange))
            guard range == expectedRange else { return }
            metrics = fetchedMetrics
            state = .loaded
        } catch let error as VouchaError {
            guard range == expectedRange else { return }
            state = .error(error)
        } catch {
            guard range == expectedRange else { return }
            state = .error(.api(statusCode: 0, preconditionCode: nil))
        }
    }

    func loadIfNeeded() async {
        guard metrics == nil else { return }
        await load()
    }

    func selectRange(_ nextRange: GrowthRange) async {
        guard nextRange != range || metrics == nil else { return }
        range = nextRange
        await load()
    }
}
