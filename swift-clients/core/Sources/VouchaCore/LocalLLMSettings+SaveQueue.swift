import Foundation

extension LocalLLMSettingsStore {
    func enqueueVoid(_ operation: @escaping @Sendable () async -> Void) async {
        let queue = await LocalLLMSettingsSaveQueueRegistry.shared.queue(for: fileURL)
        await queue.enqueueVoid(operation)
    }

    func enqueue(_ operation: @escaping @Sendable () async -> Bool) async -> Bool {
        let queue = await LocalLLMSettingsSaveQueueRegistry.shared.queue(for: fileURL)
        return await queue.enqueue(operation)
    }
}
