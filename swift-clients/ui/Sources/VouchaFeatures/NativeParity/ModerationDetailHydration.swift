/// Hydrates one bounded queue page, keeping at most four detail requests in flight.
func hydrateModerationDetails<Summary: Sendable, Detail: Sendable>(
    _ summaries: [Summary],
    fetch: @escaping @Sendable (Summary) async throws -> Detail
) async throws -> [Detail] {
    try await withThrowingTaskGroup(of: (Int, Detail).self) { group in
        var nextIndex = 0
        for index in 0 ..< min(4, summaries.count) {
            group.addTask {
                try Task.checkCancellation()
                let detail = try await fetch(summaries[index])
                return (index, detail)
            }
            nextIndex += 1
        }
        var completed: [(Int, Detail)] = []
        while let result = try await group.next() {
            completed.append(result)
            if nextIndex < summaries.count {
                try Task.checkCancellation()
                let index = nextIndex
                nextIndex += 1
                group.addTask {
                    try Task.checkCancellation()
                    let detail = try await fetch(summaries[index])
                    return (index, detail)
                }
            }
        }
        return completed.sorted { $0.0 < $1.0 }.map(\.1)
    }
}
