extension SourcesListViewModel {
    func rollbackPosition(for sourceId: String) -> SourceRollbackPosition {
        guard let index = yourSources.firstIndex(where: { $0.id == sourceId }) else {
            return SourceRollbackPosition(previousSourceId: nil, nextSourceId: nil, fallbackIndex: nil)
        }

        return SourceRollbackPosition(
            previousSourceId: index > yourSources.startIndex ? yourSources[index - 1].id : nil,
            nextSourceId: yourSources.index(after: index) < yourSources.endIndex ? yourSources[index + 1].id : nil,
            fallbackIndex: index
        )
    }

    func rollbackIndex(from position: SourceRollbackPosition?) -> Int? {
        guard let position else { return nil }
        if let nextSourceId = position.nextSourceId,
           let nextIndex = yourSources.firstIndex(where: { $0.id == nextSourceId }) {
            return nextIndex
        }
        if let previousSourceId = position.previousSourceId,
           let previousIndex = yourSources.firstIndex(where: { $0.id == previousSourceId }) {
            return yourSources.index(after: previousIndex)
        }
        if let fallbackIndex = position.fallbackIndex {
            return min(fallbackIndex, yourSources.count)
        }
        return nil
    }
}
