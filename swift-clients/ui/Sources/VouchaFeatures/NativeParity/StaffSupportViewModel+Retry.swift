import VouchaLocalization

extension StaffSupportViewModel {
    enum RetryOperation {
        case reload(ListCursorProvenance)
        case loadMore(ListCursorProvenance, cursor: String)
        case messagePage(ThreadSelection, cursor: String)
        case thread(String)
        case contact(String, after: String?)
    }

    @discardableResult
    func retry() async -> Bool {
        if let retryOperation {
            return await retry(retryOperation)
        }
        if let activeThreadSelection {
            return await selectThread(activeThreadSelection.id).didLoad
        } else if let activeContactSelection {
            return await selectContact(activeContactSelection.id)
        } else {
            await reloadList()
            return false
        }
    }

    func beginErrorOperation() -> Int {
        errorOperationGeneration += 1
        retryOperation = nil
        errorMessage = nil
        return errorOperationGeneration
    }

    func recordError(_ error: Error, for operation: RetryOperation, generation: Int) {
        recordError(.verbatim(error.localizedDescription), for: operation, generation: generation)
    }

    func recordError(_ message: UiVerbatimText, for operation: RetryOperation, generation: Int) {
        guard generation == errorOperationGeneration else { return }
        retryOperation = operation
        errorMessage = message
    }

    private func retry(_ operation: RetryOperation) async -> Bool {
        switch operation {
        case .reload:
            await reloadList()
            return false
        case let .loadMore(provenance, cursor):
            guard provenance == currentListCursorProvenance,
                  currentListCursor == cursor
            else {
                await reloadList()
                return false
            }
            await loadMoreList()
            return false
        case let .messagePage(selection, cursor):
            guard isCurrent(selection), messagePageInfo?.endCursor == cursor else { return false }
            let errorGeneration = beginErrorOperation()
            await performLoadOlderMessages(selection, cursor: cursor, errorGeneration: errorGeneration)
            return false
        case let .thread(id):
            return await selectThread(id).didLoad
        case let .contact(id, after):
            return await selectContact(id, after: after)
        }
    }

    private var currentListCursor: String? {
        switch mode {
        case .threads: threadPageInfo?.endCursor
        case .contacts: contactPageInfo?.endCursor
        }
    }
}
