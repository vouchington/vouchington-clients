import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaTestSupport

class AndroidTranscriptURLProtocol: URLProtocol {
    struct ResponsePlan {
        let statusCode: Int
        let data: Data
        let held: Bool
        let responseBuilder: (@Sendable (URLRequest) -> Data)?

        func responseData(for request: URLRequest) -> Data {
            responseBuilder?(request) ?? data
        }
    }

    private static let installLock = NSLock()
    private nonisolated(unsafe) static var installedRecorders: [String: AndroidTranscriptRecorder] = [:]

    private let stateLock = NSRecursiveLock()
    private var stopped = false
    private var requestOccurrence = 0
    private var recorder: AndroidTranscriptRecorder?

    static func install(_ recorder: AndroidTranscriptRecorder, host: String) {
        installLock.withLock { installedRecorders[host] = recorder }
    }

    static func uninstall(host: String) {
        _ = installLock.withLock { installedRecorders.removeValue(forKey: host) }
    }

    override class func canInit(with request: URLRequest) -> Bool {
        request.url?.host?.hasSuffix(".transcript.test") == true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        guard let host = request.url?.host,
              let recorder = Self.installLock.withLock({ Self.installedRecorders[host] })
        else { return }
        self.recorder = recorder
        var recordedRequest = request
        recordedRequest.httpBody = URLRequestHTTPBody.data(from: request)
        let (plan, occurrence) = recorder.start(recordedRequest, owner: self)
        requestOccurrence = occurrence
        guard !plan.held else { return }
        deliver(statusCode: plan.statusCode, data: plan.responseData(for: recordedRequest))
    }

    override func stopLoading() {
        stateLock.withLock { stopped = true }
    }

    func deliver(statusCode: Int, data: Data) {
        stateLock.lock()
        defer { stateLock.unlock() }
        guard !stopped, let client, let url = request.url
        else { return }
        guard let response = HTTPURLResponse(
            url: url,
            statusCode: statusCode,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        ) else { return }
        client.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        guard !stopped else { return }
        client.urlProtocol(self, didLoad: data)
        guard !stopped else { return }
        stopped = true
        client.urlProtocolDidFinishLoading(self)
    }
}
