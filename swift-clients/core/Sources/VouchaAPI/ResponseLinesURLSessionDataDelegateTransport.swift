import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

enum ResponseLinesMode {
    case undetermined
    case streaming
    case buffering
}

final class ResponseLineBuffer {
    private var data = Data()

    func append(_ chunk: Data) -> [String] {
        data.append(chunk)
        var lines: [String] = []
        while let newlineIndex = data.firstIndex(of: 0x0A) {
            let lineData = data[..<newlineIndex]
            if let line = String(data: Data(lineData), encoding: .utf8) {
                lines.append(line)
            }
            data.removeSubrange(data.startIndex ..< data.index(after: newlineIndex))
        }
        return lines
    }

    func flush() -> [String] {
        guard !data.isEmpty else { return [] }
        defer { data.removeAll(keepingCapacity: true) }
        guard let line = String(data: data, encoding: .utf8) else { return [] }
        return [line]
    }
}

final class ResponseLinesTransport: NSObject, URLSessionDataDelegate, @unchecked Sendable {
    let lock = NSLock()
    private let delegateQueue: OperationQueue
    var response: URLResponse?
    var responseError: Error?
    var bufferedBody = Data()
    var bodyCompleted = false
    var responseContinuation: CheckedContinuation<URLResponse, Error>?
    var bodyContinuation: CheckedContinuation<Data, Error>?
    var linesContinuation: AsyncThrowingStream<String, Error>.Continuation?
    var session: URLSession?
    var task: URLSessionDataTask?
    var mode = ResponseLinesMode.undetermined
    var lineBuffer = ResponseLineBuffer()
    var cleanupPerformed = false

    override init() {
        delegateQueue = OperationQueue()
        delegateQueue.maxConcurrentOperationCount = 1
        super.init()
    }

    func start(request: URLRequest, configuration: URLSessionConfiguration) async throws -> ResponseLines {
        let lines = makeLineStream()
        let session = URLSession(configuration: configuration, delegate: self, delegateQueue: delegateQueue)
        let task = session.dataTask(with: request)
        setTransportState(session: session, task: task)
        task.resume()

        return try await withTaskCancellationHandler {
            let response = try await waitForResponse()
            if let http = response as? HTTPURLResponse, !(200 ... 299).contains(http.statusCode) {
                do {
                    let body = try await waitForBufferedBody()
                    cleanup()
                    return ResponseLines(lines: makeLineStream(from: Data()), response: response, body: body)
                } catch {
                    cleanup()
                    throw mapError(error)
                }
            }

            return ResponseLines(lines: lines, response: response, body: Data())
        } onCancel: {
            cancel()
        }
    }

    func urlSession(
        _: URLSession,
        dataTask _: URLSessionDataTask,
        didReceive response: URLResponse,
        completionHandler: @escaping (URLSession.ResponseDisposition) -> Void
    ) {
        lock.lock()
        self.response = response
        mode = isSuccessful(response: response) ? .streaming : .buffering
        let responseContinuation = responseContinuation
        self.responseContinuation = nil
        lock.unlock()

        responseContinuation?.resume(returning: response)
        completionHandler(.allow)
    }

    func urlSession(_: URLSession, dataTask _: URLSessionDataTask, didReceive data: Data) {
        lock.lock()
        let mode = mode
        if mode != .streaming {
            bufferedBody.append(data)
            lock.unlock()
            return
        }
        let lines = lineBuffer.append(data)
        let linesContinuation = linesContinuation
        lock.unlock()

        for line in lines {
            linesContinuation?.yield(line)
        }
    }

    func urlSession(_: URLSession, task _: URLSessionTask, didCompleteWithError error: Error?) {
        lock.lock()
        let mappedError = error.map(mapError)
        let mode = mode
        let response = response
        let bufferedBody = bufferedBody
        bodyCompleted = true
        responseError = mappedError
        let responseContinuation = responseContinuation
        self.responseContinuation = nil
        let bodyContinuation = bodyContinuation
        self.bodyContinuation = nil
        lock.unlock()

        finish(
            mode: mode,
            response: response,
            bufferedBody: bufferedBody,
            error: mappedError,
            continuations: ResponseLinesContinuations(
                response: responseContinuation,
                body: bodyContinuation
            )
        )
        cleanup()
    }

    private func setTransportState(session: URLSession, task: URLSessionDataTask) {
        lock.lock()
        self.session = session
        self.task = task
        lock.unlock()
    }

    private func makeLineStream() -> AsyncThrowingStream<String, Error> {
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

    private func waitForResponse() async throws -> URLResponse {
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

    private func waitForBufferedBody() async throws -> Data {
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
