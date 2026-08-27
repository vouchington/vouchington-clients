import Foundation

actor LocalLLMSettingsSaveQueue {
    private var tail: Task<Void, Never>?
    private var generation = 0

    func enqueueVoid(_ operation: @escaping @Sendable () async -> Void) async {
        let previous = tail
        generation += 1
        let currentGeneration = generation
        let current = Task {
            await previous?.value
            await operation()
        }
        tail = current
        await current.value
        if generation == currentGeneration {
            tail = nil
        }
    }

    func enqueue(_ operation: @escaping @Sendable () async -> Bool) async -> Bool {
        let result = LocalLLMSettingsSaveResult()
        let previous = tail
        generation += 1
        let currentGeneration = generation
        let current = Task {
            await previous?.value
            let operationResult = await operation()
            await result.set(operationResult)
        }
        tail = current
        await current.value
        if generation == currentGeneration {
            tail = nil
        }
        return await result.value
    }
}

private actor LocalLLMSettingsSaveResult {
    private var storedValue = false

    var value: Bool {
        storedValue
    }

    func set(_ value: Bool) {
        storedValue = value
    }
}

actor LocalLLMSettingsSaveQueueRegistry {
    static let shared = LocalLLMSettingsSaveQueueRegistry()

    private var queues: [URL: LocalLLMSettingsSaveQueue] = [:]

    func queue(for fileURL: URL) -> LocalLLMSettingsSaveQueue {
        if let queue = queues[fileURL] {
            return queue
        }
        let queue = LocalLLMSettingsSaveQueue()
        queues[fileURL] = queue
        return queue
    }
}
