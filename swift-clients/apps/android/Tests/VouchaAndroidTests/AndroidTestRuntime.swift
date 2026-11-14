@testable import VouchaAndroid
import VouchaCore

enum TestError: Error {
    case failed
}

actor FakeAICoreRuntime: AndroidAICoreRunning {
    var state: AndroidModelState
    let response: String
    let error: Error?
    var downloadCallCount = 0
    var generateCallCount = 0
    var lastPrompt: String?

    init(state: AndroidModelState, response: String = "", error: Error? = nil) {
        self.state = state
        self.response = response
        self.error = error
    }

    func status() async throws -> AndroidModelState {
        state
    }

    func download() async throws -> AndroidModelState {
        downloadCallCount += 1
        state = .available
        return state
    }

    func generate(prompt: String) async throws -> String {
        generateCallCount += 1
        lastPrompt = prompt
        if let error {
            throw error
        }
        return response
    }

    func generationSnapshot() -> (callCount: Int, lastPrompt: String?) {
        (generateCallCount, lastPrompt)
    }

    func downloadCount() -> Int {
        downloadCallCount
    }
}
