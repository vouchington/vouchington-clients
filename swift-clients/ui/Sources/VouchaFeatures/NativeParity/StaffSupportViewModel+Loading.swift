import VouchaAPI
import VouchaModels

extension StaffSupportViewModel {
    func reloadList() async {
        let provenance = currentListCursorProvenance
        let errorGeneration = beginErrorOperation()
        await performReloadList(provenance: provenance, errorGeneration: errorGeneration)
    }

    func performReloadList(provenance: ListCursorProvenance, errorGeneration: Int) async {
        guard let client else { return }
        let generation = beginListLoad()
        isListLoading = true
        defer {
            if isCurrentListLoad(generation) {
                isListLoading = false
            }
        }
        do {
            switch mode {
            case .threads:
                let response: SupportThreadListResponse = try await client.send(
                    .staffSupportThreads(query: provenance.query, status: provenance.status)
                )
                guard isCurrentListLoad(generation, provenance: provenance) else { return }
                threads = response.results
                threadPageInfo = response.pageInfo
                threadPageCursorProvenance = provenance
            case .contacts:
                let response: SupportContactListResponse = try await client.send(
                    .staffSupportContacts(query: provenance.query)
                )
                guard isCurrentListLoad(generation, provenance: provenance) else { return }
                contacts = response.results
                contactPageInfo = response.pageInfo
                contactPageCursorProvenance = provenance
            }
        } catch {
            guard isCurrentListLoad(generation, provenance: provenance) else { return }
            recordError(error, for: .reload(provenance), generation: errorGeneration)
        }
    }

    func loadMoreList() async {
        guard let client, !isListLoading, canLoadMoreList else { return }
        let generation = listLoadGeneration
        let provenance = currentListCursorProvenance
        let errorGeneration = beginErrorOperation()
        let cursor: String
        switch mode {
        case .threads:
            guard let value = threadPageInfo?.endCursor else { return }
            cursor = value
        case .contacts:
            guard let value = contactPageInfo?.endCursor else { return }
            cursor = value
        }
        await performLoadMoreList(
            client: client,
            generation: generation,
            provenance: provenance,
            cursor: cursor,
            errorGeneration: errorGeneration
        )
    }

    func performLoadMoreList(
        client: APIClient,
        generation: Int,
        provenance: ListCursorProvenance,
        cursor: String,
        errorGeneration: Int
    ) async {
        isListLoading = true
        defer {
            if isCurrentListLoad(generation) {
                isListLoading = false
            }
        }
        do {
            switch mode {
            case .threads:
                let response: SupportThreadListResponse = try await client.send(
                    .staffSupportThreads(query: provenance.query, status: provenance.status, after: cursor)
                )
                guard isCurrentListLoad(generation, provenance: provenance) else { return }
                threads = merge(existing: threads, incoming: response.results)
                threadPageInfo = response.pageInfo
                threadPageCursorProvenance = provenance
            case .contacts:
                let response: SupportContactListResponse = try await client.send(
                    .staffSupportContacts(query: provenance.query, after: cursor)
                )
                guard isCurrentListLoad(generation, provenance: provenance) else { return }
                contacts = merge(existing: contacts, incoming: response.results)
                contactPageInfo = response.pageInfo
                contactPageCursorProvenance = provenance
            }
        } catch {
            guard isCurrentListLoad(generation, provenance: provenance) else { return }
            recordError(error, for: .loadMore(provenance, cursor: cursor), generation: errorGeneration)
        }
    }

    @discardableResult
    func selectThread(_ id: String, preservingComposerState: Bool = false) async -> ThreadLoadResult {
        guard let client else { return .init(selection: nil, didLoad: false) }
        let errorGeneration = beginErrorOperation()
        let selection = beginThreadSelection(
            id,
            errorOperationGeneration: errorGeneration,
            preservingComposerState: preservingComposerState
        )
        isDetailLoading = true
        defer {
            if isCurrent(selection) {
                isDetailLoading = false
            }
        }
        do {
            async let threadResponse: SupportThreadResponse = client.send(.staffSupportThread(threadId: id))
            async let messageResponse: SupportMessageListResponse = client.send(.staffSupportMessages(threadId: id))
            let (thread, messagePage) = try await (threadResponse, messageResponse)
            guard isCurrent(selection) else { return .init(selection: selection, didLoad: false) }
            selectedThread = thread.thread
            replaceThread(thread.thread)
            messages = messagePage.results
            messagePageInfo = messagePage.pageInfo
            if !preservingComposerState {
                draftEdits = [:]
            }
            return .init(selection: selection, didLoad: true)
        } catch {
            guard isCurrent(selection) else { return .init(selection: selection, didLoad: false) }
            recordError(error, for: .thread(id), generation: selection.errorOperationGeneration)
            return .init(selection: selection, didLoad: false)
        }
    }

    @discardableResult
    func selectContact(_ id: String, after: String? = nil) async -> Bool {
        guard let client else { return false }
        let selection: ContactSelection
        if after == nil {
            selection = beginContactSelection(id)
        } else if let activeContactSelection, activeContactSelection.id == id {
            selection = activeContactSelection
        } else {
            return false
        }
        isDetailLoading = true
        let errorGeneration = beginErrorOperation()
        defer {
            if isCurrent(selection) {
                isDetailLoading = false
            }
        }
        do {
            let response: SupportContactDetailResponse = try await client.send(
                .staffSupportContact(contactId: id, after: after)
            )
            guard isCurrent(selection) else { return false }
            selectedContact = response.contact
            if after == nil {
                contactThreads = response.threads
            } else {
                contactThreads = merge(existing: contactThreads, incoming: response.threads)
            }
            contactThreadPageInfo = response.threadPageInfo
            return true
        } catch {
            guard isCurrent(selection) else { return false }
            recordError(error, for: .contact(id, after: after), generation: errorGeneration)
            return false
        }
    }

    func replaceThread(_ thread: SupportThread) {
        if !threadMatchesActiveStatusFilter(thread) {
            threads.removeAll { $0.id == thread.id }
        } else if let index = threads.firstIndex(where: { $0.id == thread.id }) {
            threads[index] = thread
        } else if normalizedQuery == nil {
            threads.insert(thread, at: 0)
        }
        if selectedThread?.id == thread.id {
            selectedThread = thread
        }
    }

}
