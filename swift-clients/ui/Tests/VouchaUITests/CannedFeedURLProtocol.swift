import Foundation

/// A `URLProtocol` that serves canned responses keyed by URL path and captures requested URLs.
final class CannedFeedURLProtocol: URLProtocol {
    struct CapturedRequest {
        let sequence: UInt64
        let url: URL
        let method: String
        let body: String?
    }

    struct RequestBarrier {
        private let path: String
        private let method: String
        private let baselineSequence: UInt64

        init(path: String, method: String, baselineSequence: UInt64) {
            self.path = path
            self.method = method
            self.baselineSequence = baselineSequence
        }

        func wait(timeout: Duration = .seconds(1)) async throws -> CapturedRequest {
            let clock = ContinuousClock()
            let deadline = clock.now.advanced(by: timeout)

            while true {
                try Task.checkCancellation()
                if let request = CannedFeedURLProtocol.suspendedCapturedRequest(
                    after: baselineSequence,
                    path: path,
                    method: method
                ) {
                    return request
                }
                guard clock.now < deadline else {
                    throw RequestBarrierTimeout(path: path, method: method, timeout: timeout)
                }
                try await clock.sleep(for: .milliseconds(10))
            }
        }
    }

    private struct RequestBarrierTimeout: LocalizedError {
        let path: String
        let method: String
        let timeout: Duration

        var errorDescription: String? {
            "Timed out after \(timeout) waiting for suspended \(method) request to \(path)"
        }
    }

    private struct PendingResponse {
        let sequence: UInt64
        let protocolInstance: CannedFeedURLProtocol
        let path: String
        let data: Data
        let status: Int
        let delay: TimeInterval

        func deliver() {
            protocolInstance.deliver(path: path, data: data, status: status, delay: delay)
        }
    }

    private static let lock = NSLock()
    private static var _handlers: [String: (Data, Int)] = [:]
    private static var _queuedHandlers: [String: [(Data, Int, TimeInterval)]] = [:]
    private static var _errors: [String: Error] = [:]
    private static var _capturedRequests: [CapturedRequest] = []
    private static var capturedRequestSequence: UInt64 = 0
    private static var suspendedResponsePaths: Set<String> = []
    private static var responseSuspendedCallbacks: [String: () -> Void] = [:]
    private static var pendingResponses: [String: [PendingResponse]] = [:]
    private static var _contentTypes: [String: String] = [:]

    static var handlers: [String: (Data, Int)] {
        get { withLock { _handlers } }
        set { withLock { _handlers = newValue } }
        _modify {
            lock.lock()
            defer { lock.unlock() }
            yield &_handlers
        }
    }

    static var queuedHandlers: [String: [(Data, Int, TimeInterval)]] {
        get { withLock { _queuedHandlers } }
        set { withLock { _queuedHandlers = newValue } }
        _modify {
            lock.lock()
            defer { lock.unlock() }
            yield &_queuedHandlers
        }
    }

    static var errors: [String: Error] {
        get { withLock { _errors } }
        set { withLock { _errors = newValue } }
        _modify {
            lock.lock()
            defer { lock.unlock() }
            yield &_errors
        }
    }

    static var capturedURLs: [URL] {
        get { withLock { _capturedRequests.map(\.url) } }
        set { resetCapturedRequests(replacementIsEmpty: newValue.isEmpty) }
    }

    static var capturedMethods: [String] {
        get { withLock { _capturedRequests.map(\.method) } }
        set { resetCapturedRequests(replacementIsEmpty: newValue.isEmpty) }
    }

    static var capturedBodies: [String?] {
        get { withLock { _capturedRequests.map(\.body) } }
        set { resetCapturedRequests(replacementIsEmpty: newValue.isEmpty) }
    }

    static var capturedRequests: [CapturedRequest] {
        withLock { _capturedRequests }
    }

    static func requestBarrier(path: String, method: String) -> RequestBarrier {
        withLock {
            RequestBarrier(
                path: path,
                method: method,
                baselineSequence: capturedRequestSequence
            )
        }
    }

    static var contentTypes: [String: String] {
        get { withLock { _contentTypes } }
        set { withLock { _contentTypes = newValue } }
        _modify {
            lock.lock()
            defer { lock.unlock() }
            yield &_contentTypes
        }
    }

    private let stateLock = NSLock()
    private var stopped = false

    /// True once `stopLoading()` has run. `deliver(...)` rechecks this before constructing the
    /// response so a delayed delivery woken after cancellation can bail out early; the actual
    /// client callbacks go through `deliverIfNotStopped(_:)` instead, which checks and calls
    /// under the same lock so a `stopLoading()` racing the check can't slip a callback through.
    private var isStopped: Bool {
        stateLock.lock()
        defer { stateLock.unlock() }
        return stopped
    }

    /// Runs `callback` only if `stopLoading()` hasn't run yet, with the check and the callback
    /// itself under `stateLock` — a plain `guard !isStopped else { return }` before the callback
    /// leaves a gap where `stopLoading()` can run between the check and the call, still
    /// delivering into a torn-down client. `client` is CFNetwork-internal and never re-enters
    /// `stopLoading()` synchronously, so holding the lock across the call is safe.
    private func deliverIfNotStopped(_ callback: () -> Void) {
        stateLock.lock()
        defer { stateLock.unlock() }
        guard !stopped else { return }
        callback()
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    /// Invoked immediately before `deliver(...)` enters its delay sleep. Test-only: lets a test
    /// synchronize a concurrent `stopLoading()` call to the exact moment delivery starts waiting,
    /// instead of racing a fixed timer that can fire late under CI runner load.
    var onWillSleepForDelivery: (() -> Void)?

    /// Invoked after the shared capture lock is acquired but before the request is recorded.
    /// Test-only: lets a regression test prove that resets use the same lock as capture.
    var onWillCaptureRequest: (() -> Void)?

    override func startLoading() {
        let path = request.url?.path ?? ""
        if let error = Self.error(for: request, path: path) {
            deliverIfNotStopped { client?.urlProtocol(self, didFailWithError: error) }
            return
        }
        let (data, status, delay, sequence) = Self.response(
            for: request,
            path: path,
            onWillCaptureRequest: onWillCaptureRequest
        )
        if Self.suspendResponseIfNeeded(
            protocolInstance: self,
            path: path,
            data: data,
            status: status,
            delay: delay,
            sequence: sequence
        ) {
            return
        }
        deliver(path: path, data: data, status: status, delay: delay)
    }

    private func deliver(path: String, data: Data, status: Int, delay: TimeInterval) {
        if delay > 0 {
            onWillSleepForDelivery?()
            Thread.sleep(forTimeInterval: delay)
        }
        guard !isStopped else { return }
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: status,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": Self.contentTypes[path] ?? "application/json"]
        )!
        deliverIfNotStopped { client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed) }
        deliverIfNotStopped { client?.urlProtocol(self, didLoad: data) }
        deliverIfNotStopped { client?.urlProtocolDidFinishLoading(self) }
    }

    override func stopLoading() {
        stateLock.lock()
        stopped = true
        stateLock.unlock()
    }

    static func capturedPathCount(_ path: String) -> Int {
        withLock { _capturedRequests.count { $0.url.path == path } }
    }

    static func hasCapturedRequest(path: String) -> Bool {
        withLock { _capturedRequests.contains { $0.url.path == path } }
    }

    static func reset() {
        withLock {
            _handlers = [:]
            _queuedHandlers = [:]
            _errors = [:]
            _contentTypes = [:]
            resetCapturedRequestsLocked()
            capturedRequestSequence = 0
            suspendedResponsePaths = []
            responseSuspendedCallbacks = [:]
            pendingResponses = [:]
        }
    }

    static func resetCapturedRequests(onWillAcquireLock: (() -> Void)? = nil) {
        onWillAcquireLock?()
        withLock {
            resetCapturedRequestsLocked()
        }
    }

    static func suspendResponse(path: String, onSuspend: (() -> Void)? = nil) {
        lock.lock()
        defer { lock.unlock() }
        suspendedResponsePaths.insert(path)
        responseSuspendedCallbacks[path] = onSuspend
    }

    static func hasSuspendedResponse(path: String, minimumCount: Int = 1) -> Bool {
        lock.lock()
        defer { lock.unlock() }
        return pendingResponses[path, default: []].count >= minimumCount
    }

    static func releaseNewestResponse(path: String) {
        lock.lock()
        guard var responses = pendingResponses[path], !responses.isEmpty else {
            lock.unlock()
            preconditionFailure("No pending response to release for path: \(path)")
        }
        let response = responses.removeLast()
        if responses.isEmpty {
            pendingResponses.removeValue(forKey: path)
        } else {
            pendingResponses[path] = responses
        }
        lock.unlock()
        response.deliver()
    }

    static func releaseOldestResponse(path: String) {
        lock.lock()
        guard var responses = pendingResponses[path], !responses.isEmpty else {
            lock.unlock()
            preconditionFailure("No pending response to release for path: \(path)")
        }
        let response = responses.removeFirst()
        if responses.isEmpty {
            pendingResponses.removeValue(forKey: path)
        } else {
            pendingResponses[path] = responses
        }
        lock.unlock()
        response.deliver()
    }

    static func suspendedResponseCount(path: String) -> Int {
        lock.lock()
        defer { lock.unlock() }
        return pendingResponses[path]?.count ?? 0
    }

    static func releaseResponse(path: String) {
        lock.lock()
        suspendedResponsePaths.remove(path)
        let responses = pendingResponses.removeValue(forKey: path) ?? []
        lock.unlock()
        for pendingResponse in responses {
            pendingResponse.deliver()
        }
    }

    /// Drops any suspended-but-unreleased responses without delivering them. A response a test
    /// suspended and forgot to release would otherwise sit until some unrelated later test's
    /// `releaseResponse` call resurrected it and delivered into that earlier test's
    /// already-torn-down client — these dictionaries are class-level, so this must never call
    /// `deliver`. Call from `tearDown`.
    static func discardPendingResponses() {
        lock.lock()
        defer { lock.unlock() }
        suspendedResponsePaths = []
        responseSuspendedCallbacks = [:]
        pendingResponses = [:]
    }

    private static func error(for request: URLRequest, path: String) -> Error? {
        lock.lock()
        defer { lock.unlock() }
        guard let error = _errors[path] else { return nil }
        _ = capture(request)
        return error
    }

    private static func response(
        for request: URLRequest,
        path: String,
        onWillCaptureRequest: (() -> Void)?
    ) -> (Data, Int, TimeInterval, UInt64) {
        lock.lock()
        defer { lock.unlock() }
        onWillCaptureRequest?()
        let sequence = capture(request)
        if var queued = _queuedHandlers[path], !queued.isEmpty {
            let next = queued.removeFirst()
            _queuedHandlers[path] = queued
            return (next.0, next.1, next.2, sequence)
        }
        let (data, status) = _handlers[path] ?? (Data("{}".utf8), 200)
        return (data, status, 0, sequence)
    }

    private static func suspendResponseIfNeeded(
        protocolInstance: CannedFeedURLProtocol,
        path: String,
        data: Data,
        status: Int,
        delay: TimeInterval,
        sequence: UInt64
    ) -> Bool {
        lock.lock()
        guard suspendedResponsePaths.contains(path) else {
            lock.unlock()
            return false
        }
        pendingResponses[path, default: []].append(PendingResponse(
            sequence: sequence,
            protocolInstance: protocolInstance,
            path: path,
            data: data,
            status: status,
            delay: delay
        ))
        let callback = responseSuspendedCallbacks.removeValue(forKey: path)
        lock.unlock()
        callback?()
        return true
    }

    private static func capture(_ request: URLRequest) -> UInt64 {
        capturedRequestSequence &+= 1
        let sequence = capturedRequestSequence
        guard let url = request.url else { return sequence }
        _capturedRequests.append(CapturedRequest(
            sequence: sequence,
            url: url,
            method: request.httpMethod ?? "GET",
            body: capturedBody(from: request)
        ))
        return sequence
    }

    private static func resetCapturedRequestsLocked() {
        _capturedRequests = []
    }

    private static func suspendedCapturedRequest(
        after baselineSequence: UInt64,
        path: String,
        method: String
    ) -> CapturedRequest? {
        withLock {
            _capturedRequests.first { request in
                request.sequence > baselineSequence
                    && request.url.path == path
                    && request.method == method
                    && pendingResponses[path, default: []].contains { $0.sequence == request.sequence }
            }
        }
    }

    private static func resetCapturedRequests(replacementIsEmpty: Bool) {
        precondition(replacementIsEmpty, "Captured requests can only be cleared")
        resetCapturedRequests()
    }

    private static func withLock<T>(_ operation: () -> T) -> T {
        lock.lock()
        defer { lock.unlock() }
        return operation()
    }

    private static func capturedBody(from request: URLRequest) -> String? {
        if let body = request.httpBody {
            return String(data: body, encoding: .utf8)
        }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let bufferSize = 1_024
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: bufferSize)
        defer { buffer.deallocate() }
        while stream.hasBytesAvailable {
            let read = stream.read(buffer, maxLength: bufferSize)
            if read <= 0 {
                break
            }
            data.append(buffer, count: read)
        }
        return String(data: data, encoding: .utf8)
    }
}
