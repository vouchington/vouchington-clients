import Foundation

@MainActor
extension PaymentCardsViewModel {
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
            let results = try await service.searchCardTopics(query: trimmed)
            guard searchGeneration == generation,
                  topicQuery.trimmingCharacters(in: .whitespacesAndNewlines) == trimmed
            else { return }
            topicResults = results
            searchErrorMessage = nil
        } catch {
            guard searchGeneration == generation else { return }
            searchErrorMessage = .verbatim(error.localizedDescription)
        }
    }
}
