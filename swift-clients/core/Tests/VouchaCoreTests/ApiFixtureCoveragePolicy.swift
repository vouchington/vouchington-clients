/// A manifest fixture a client is expected to account for, with the producer's `pending` flag.
struct FixtureCoverageCandidate: Equatable {
    let id: String
    let pending: Bool
}

/// Pending-aware rules shared by the fixture coverage gates. A producer may ship a fixture marked
/// `pending: true` before any client uses it; only unregistered fixtures are exempted.
enum ApiFixtureCoveragePolicy {
    /// Ids that must be registered but are not: unregistered fixtures that are not pending.
    static func unregistered(_ candidates: [FixtureCoverageCandidate], registered: Set<String>) -> [String] {
        candidates
            .filter { !$0.pending && !registered.contains($0.id) }
            .map(\.id)
            .sorted()
    }

    /// Runs every registered fixture's shape/decoding check regardless of `pending`.
    static func roundTripFailures(_ registry: [RegisteredFixture]) -> [String] {
        registry.compactMap { fixture in
            do {
                try fixture.run(fixture.id)
                return nil
            } catch {
                return "\(fixture.id): \(error)"
            }
        }
    }
}
