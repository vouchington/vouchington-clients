import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

extension ResponseLinesTransport {
    func setTransportState(session: URLSession, task: URLSessionDataTask) {
        lock.lock()
        self.session = session
        self.task = task
        lock.unlock()
    }

    func makeLineStream() -> AsyncThrowingStream<String, Error> {
        AsyncThrowingStream { continuation in
            lock.lock()
            linesContinuation = continuation
            lock.unlock()
            continuation.onTermination = { [weak self] termination in
                if case .cancelled = termination {
                    self?.cancel()
                }
            }
        }
    }

    func waitForResponse() async throws -> URLResponse {
        try await withCheckedThrowingContinuation { continuation in
            lock.lock()
            if let response {
                lock.unlock()
                continuation.resume(returning: response)
            } else if let responseError {
                lock.unlock()
                continuation.resume(throwing: responseError)
            } else {
                responseContinuation = continuation
                lock.unlock()
            }
        }
    }

    func waitForBufferedBody() async throws -> Data {
        try await withCheckedThrowingContinuation { continuation in
            lock.lock()
            if bodyCompleted, let responseError {
                lock.unlock()
                continuation.resume(throwing: responseError)
            } else if bodyCompleted {
                let body = bufferedBody
                lock.unlock()
                continuation.resume(returning: body)
            } else {
                bodyContinuation = continuation
                lock.unlock()
            }
        }
    }
}
