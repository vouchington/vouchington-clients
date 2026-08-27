import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
import VouchaCore

struct ResponseLinesContinuations {
    let response: CheckedContinuation<URLResponse, Error>?
    let body: CheckedContinuation<Data, Error>?
}

extension ResponseLinesTransport {
    func finish(
        mode: ResponseLinesMode,
        response: URLResponse?,
        bufferedBody: Data,
        error: Error?,
        continuations: ResponseLinesContinuations
    ) {
        switch mode {
        case .streaming:
            finishLines(throwing: error)
        case .buffering:
            finishBody(bufferedBody, error: error, continuation: continuations.body)
        case .undetermined:
            finishUndetermined(
                response: response,
                bufferedBody: bufferedBody,
                error: error,
                continuations: continuations
            )
        }
    }

    func finishUndetermined(
        response: URLResponse?,
        bufferedBody: Data,
        error: Error?,
        continuations: ResponseLinesContinuations
    ) {
        if let error {
            continuations.response?.resume(throwing: error)
            continuations.body?.resume(throwing: error)
            finishLines(throwing: error)
        } else if let response {
            continuations.response?.resume(returning: response)
            continuations.body?.resume(returning: bufferedBody)
            finishLines(throwing: nil)
        } else {
            let error = CancellationError()
            continuations.response?.resume(throwing: error)
            continuations.body?.resume(throwing: error)
            finishLines(throwing: error)
        }
    }

    func finishBody(
        _ body: Data,
        error: Error?,
        continuation: CheckedContinuation<Data, Error>?
    ) {
        if let error {
            continuation?.resume(throwing: error)
        } else {
            continuation?.resume(returning: body)
        }
        finishLines(throwing: nil)
    }

    func finishLines(throwing error: Error?) {
        lock.lock()
        guard let linesContinuation else {
            lock.unlock()
            return
        }
        let lines = error == nil ? lineBuffer.flush() : []
        self.linesContinuation = nil
        lock.unlock()

        if let error {
            linesContinuation.finish(throwing: error)
        } else {
            for line in lines {
                linesContinuation.yield(line)
            }
            linesContinuation.finish()
        }
    }

    func cleanup() {
        lock.lock()
        guard !cleanupPerformed else {
            lock.unlock()
            return
        }
        cleanupPerformed = true
        let session = session
        self.session = nil
        task = nil
        lock.unlock()
        session?.finishTasksAndInvalidate()
    }

    func cancel() {
        lock.lock()
        guard !cleanupPerformed else {
            lock.unlock()
            return
        }
        cleanupPerformed = true
        let error = CancellationError()
        let task = task
        let session = session
        let responseContinuation = responseContinuation
        let bodyContinuation = bodyContinuation
        let linesContinuation = linesContinuation
        responseError = error
        bodyCompleted = true
        self.task = nil
        self.session = nil
        self.responseContinuation = nil
        self.bodyContinuation = nil
        self.linesContinuation = nil
        lock.unlock()
        responseContinuation?.resume(throwing: error)
        bodyContinuation?.resume(throwing: error)
        linesContinuation?.finish(throwing: error)
        task?.cancel()
        session?.invalidateAndCancel()
    }

    func isSuccessful(response: URLResponse) -> Bool {
        guard let http = response as? HTTPURLResponse else { return true }
        return (200 ... 299).contains(http.statusCode)
    }

    func mapError(_ error: Error) -> Error {
        if let urlError = error as? URLError {
            return urlError.code == .cancelled ? CancellationError() : VouchaError.network(urlError)
        }
        return error
    }

    func makeLineStream(from data: Data) -> AsyncThrowingStream<String, Error> {
        AsyncThrowingStream { continuation in
            let lineBuffer = ResponseLineBuffer()
            for line in lineBuffer.append(data) + lineBuffer.flush() {
                continuation.yield(line)
            }
            continuation.finish()
        }
    }
}
