import Foundation

extension PointValuationsViewModel {
    func searchTopics() async {
        let trimmed = topicQuery.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            searchGeneration = UUID()
            topicResults = []
            searchErrorMessage = nil
            isSearching = false
            return
        }
        guard !isSearching else { return }
        let generation = UUID()
        searchGeneration = generation
        isSearching = true
        searchErrorMessage = nil
        defer {
            if searchGeneration == generation {
                isSearching = false
            }
        }
        do {
            let results = try await service.searchRewardsPrograms(query: trimmed)
            guard searchGeneration == generation,
                  topicQuery.trimmingCharacters(in: .whitespacesAndNewlines) == trimmed
            else { return }
            var seenIds = Set<String>()
            topicResults = results.filter {
                $0.topicType == "rewards_program" && $0.name != nil && seenIds.insert($0.id).inserted
            }
        } catch {
            guard searchGeneration == generation else { return }
            searchErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}
