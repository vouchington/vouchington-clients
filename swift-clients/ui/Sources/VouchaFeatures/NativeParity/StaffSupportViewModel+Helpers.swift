import VouchaModels

extension StaffSupportViewModel {
    func beginContactSelection(_ id: String) -> ContactSelection {
        contactSelectionGeneration += 1
        let selection = ContactSelection(id: id, generation: contactSelectionGeneration)
        activeContactSelection = selection
        selectedContact = nil
        contactThreads = []
        contactThreadPageInfo = nil
        return selection
    }

    func isCurrent(_ selection: ThreadSelection) -> Bool {
        activeThreadSelection?.id == selection.id &&
            activeThreadSelection?.generation == selection.generation
    }

    func isCurrent(_ selection: ContactSelection) -> Bool {
        activeContactSelection?.id == selection.id &&
            activeContactSelection?.generation == selection.generation
    }

    func beginListLoad() -> Int {
        listLoadGeneration += 1
        return listLoadGeneration
    }

    func isCurrentListLoad(_ generation: Int) -> Bool {
        listLoadGeneration == generation
    }

    func isCurrentListLoad(_ generation: Int, provenance: ListCursorProvenance) -> Bool {
        isCurrentListLoad(generation) && currentListCursorProvenance == provenance
    }

    var canLoadMoreList: Bool {
        switch mode {
        case .threads:
            threadPageInfo?.hasNextPage == true && threadPageCursorProvenance == currentListCursorProvenance
        case .contacts:
            contactPageInfo?.hasNextPage == true && contactPageCursorProvenance == currentListCursorProvenance
        }
    }

    var currentListCursorProvenance: ListCursorProvenance {
        ListCursorProvenance(
            query: normalizedQuery,
            status: mode == .threads ? status : nil
        )
    }

    var normalizedQuery: String? {
        let value = query.trimmingCharacters(in: .whitespacesAndNewlines)
        return value.isEmpty ? nil : value
    }

    func threadMatchesActiveStatusFilter(_ thread: SupportThread) -> Bool {
        guard let status else { return true }
        return thread.status.rawValue == status.rawValue
    }

    func merge<T: Identifiable>(existing: [T], incoming: [T]) -> [T] where T.ID: Hashable {
        var seen = Set(existing.map(\.id))
        return existing + incoming.filter { seen.insert($0.id).inserted }
    }
}
