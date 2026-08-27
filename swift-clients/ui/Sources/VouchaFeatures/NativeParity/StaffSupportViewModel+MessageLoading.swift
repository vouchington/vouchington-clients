import VouchaAPI
import VouchaModels

extension StaffSupportViewModel {
    func replaceMessage(_ message: SupportMessage) {
        if let index = messages.firstIndex(where: { $0.id == message.id }) {
            messages[index] = message
        } else {
            messages.append(message)
        }
    }

    func loadOlderMessages() async {
        guard let client,
              let selection = activeThreadSelection,
              let cursor = messagePageInfo?.endCursor
        else { return }
        let errorGeneration = beginErrorOperation()
        await performLoadOlderMessages(selection, cursor: cursor, errorGeneration: errorGeneration, client: client)
    }

    func performLoadOlderMessages(
        _ selection: ThreadSelection,
        cursor: String,
        errorGeneration: Int,
        client: APIClient? = nil
    ) async {
        guard let client = client ?? self.client else { return }
        do {
            let response: SupportMessageListResponse = try await client.send(
                .staffSupportMessages(threadId: selection.id, after: cursor)
            )
            guard isCurrent(selection) else { return }
            messages = merge(existing: response.results, incoming: messages)
            messagePageInfo = response.pageInfo
        } catch {
            guard isCurrent(selection) else { return }
            recordError(error, for: .messagePage(selection, cursor: cursor), generation: errorGeneration)
        }
    }
}
