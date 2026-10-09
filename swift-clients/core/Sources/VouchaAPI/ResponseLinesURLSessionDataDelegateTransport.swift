import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif

enum ResponseLinesMode {
    case undetermined
    case streaming
    case buffering
}

enum ResponseLineBufferError: Error, Equatable {
    case lineTooLarge, bodyTooLarge
}

final class ResponseLineBuffer {
    static let maximumLineBytes = 1_024 * 1_024
    private var data = Data()

    func append(_ chunk: Data, emit: (String) -> Void) throws {
        var start = chunk.startIndex
        while let newlineIndex = chunk[start...].firstIndex(of: 0x0A) {
            try append(chunk[start ..< newlineIndex])
            emitCurrentLine(to: emit)
            start = chunk.index(after: newlineIndex)
        }
        try append(chunk[start...])
    }

    func flush() -> String? {
        guard !data.isEmpty else { return nil }
        defer { data.removeAll(keepingCapacity: true) }
        return String(data: data, encoding: .utf8)
    }

    private func append(_ segment: Data.SubSequence) throws {
        guard data.count + segment.count <= Self.maximumLineBytes else {
            data.removeAll(keepingCapacity: false)
            throw ResponseLineBufferError.lineTooLarge
        }
        data.append(contentsOf: segment)
    }

    private func emitCurrentLine(to emit: (String) -> Void) {
        if let line = String(data: data, encoding: .utf8) {
            emit(line)
        }
        data.removeAll(keepingCapacity: true)
    }
}

final class ResponseLinesTransport: NSObject, URLSessionDataDelegate, @unchecked Sendable {
    static let maximumDiagnosticBytes = 64 * 1_024
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
            guard bufferedBody.count + data.count <= Self.maximumDiagnosticBytes else {
                lock.unlock()
                failStreaming(ResponseLineBufferError.bodyTooLarge)
                return
            }
            bufferedBody.append(data)
            lock.unlock()
            return
        }
        let linesContinuation = linesContinuation
        do {
            try lineBuffer.append(data) { line in
                linesContinuation?.yield(line)
            }
        } catch {
            lock.unlock()
            failStreaming(error)
            return
        }
        lock.unlock()
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

}
