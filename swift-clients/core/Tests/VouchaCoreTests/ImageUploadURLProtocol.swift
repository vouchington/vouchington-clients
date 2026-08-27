import Foundation

final class ImageUploadURLProtocol: URLProtocol {
    static var handlers: [String: (Data, Int)] = [:]
    static var queuedHandlers: [String: [(Data, Int, TimeInterval)]] = [:]
    static var capturedRequests: [URLRequest] = []

    private let stateLock = NSLock()
    private var stopped = false

    /// Runs `callback` only if `stopLoading()` hasn't run yet, with the check and the callback
    /// itself under `stateLock` — see `CannedFeedURLProtocol.deliverIfNotStopped(_:)` for why the
    /// check and the call must share a lock instead of a plain `guard` before the callback.
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

    /// Invoked immediately before `startLoading()` enters its delay sleep. Test-only: lets a test
    /// synchronize a concurrent `stopLoading()` call to the exact moment delivery starts waiting.
    var onWillSleepForDelivery: (() -> Void)?

    override func startLoading() {
        let path = request.url?.path ?? ""
        Self.capturedRequests.append(request)
        let responseTuple: (Data, Int, TimeInterval)
        if var queued = Self.queuedHandlers[path], !queued.isEmpty {
            responseTuple = queued.removeFirst()
            Self.queuedHandlers[path] = queued
        } else {
            let (data, status) = Self.handlers[path] ?? (Data("{}".utf8), 200)
            responseTuple = (data, status, 0)
        }

        if responseTuple.2 > 0 {
            onWillSleepForDelivery?()
            Thread.sleep(forTimeInterval: responseTuple.2)
        }
        stateLock.lock()
        let isStopped = stopped
        stateLock.unlock()
        guard !isStopped else { return }
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: responseTuple.1,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        deliverIfNotStopped { client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed) }
        deliverIfNotStopped { client?.urlProtocol(self, didLoad: responseTuple.0) }
        deliverIfNotStopped { client?.urlProtocolDidFinishLoading(self) }
    }

    override func stopLoading() {
        stateLock.lock()
        stopped = true
        stateLock.unlock()
    }

    static func capturedBody(at index: Int) -> String? {
        guard capturedRequests.indices.contains(index) else { return nil }
        let request = capturedRequests[index]
        if let body = request.httpBody {
            return String(data: body, encoding: .utf8)
        }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 1_024)
        while stream.hasBytesAvailable {
            let read = buffer.withUnsafeMutableBufferPointer { pointer in
                guard let baseAddress = pointer.baseAddress else { return 0 }
                return stream.read(baseAddress, maxLength: pointer.count)
            }
            if read <= 0 {
                break
            }
            data.append(buffer, count: read)
        }
        return String(data: data, encoding: .utf8)
    }
}
