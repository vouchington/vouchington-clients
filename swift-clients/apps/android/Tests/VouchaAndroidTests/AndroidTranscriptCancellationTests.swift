import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import XCTest

final class AndroidTranscriptCancellationTests: XCTestCase {
    func testStoppedProtocolDoesNotDeliverHeldResponse() throws {
        let client = TranscriptCallbackClient()
        let subject = try makeProtocol(client: client)

        subject.stopLoading()
        subject.deliver(statusCode: 200, data: Data("{}".utf8))

        XCTAssertEqual(client.events, [])
    }

    func testStopDuringResponsePreventsDataAndCompletionCallbacks() throws {
        let client = TranscriptCallbackClient(stopAfter: .response)
        let subject = try makeProtocol(client: client)

        subject.deliver(statusCode: 200, data: Data("{}".utf8))

        XCTAssertEqual(client.events, [.response])
    }

    func testStopDuringDataPreventsCompletionCallback() throws {
        let client = TranscriptCallbackClient(stopAfter: .data)
        let subject = try makeProtocol(client: client)

        subject.deliver(statusCode: 200, data: Data("{}".utf8))

        XCTAssertEqual(client.events, [.response, .data])
    }

    func testUninterruptedDeliveryCompletesOnce() throws {
        let client = TranscriptCallbackClient()
        let subject = try makeProtocol(client: client)

        subject.deliver(statusCode: 200, data: Data("{}".utf8))
        subject.deliver(statusCode: 200, data: Data("{}".utf8))

        XCTAssertEqual(client.events, [.response, .data, .finished])
    }

    private func makeProtocol(client: TranscriptCallbackClient) throws -> AndroidTranscriptURLProtocol {
        let url = try XCTUnwrap(URL(string: "https://callback.transcript.test/result"))
        return AndroidTranscriptURLProtocol(request: URLRequest(url: url), cachedResponse: nil, client: client)
    }
}

/// Recorded callbacks are protected by one lock; the cancellation policy is immutable.
private final class TranscriptCallbackClient: NSObject, URLProtocolClient, @unchecked Sendable {
    enum Event: Equatable {
        case response
        case data
        case finished
    }

    private let lock = NSLock()
    private var recordedEvents: [Event] = []
    private let stopAfter: Event?

    init(stopAfter: Event? = nil) {
        self.stopAfter = stopAfter
    }

    var events: [Event] {
        lock.withLock { recordedEvents }
    }

    func urlProtocol(_ subject: URLProtocol, didReceive _: URLResponse, cacheStoragePolicy _: URLCache.StoragePolicy) {
        record(.response, subject: subject)
    }

    func urlProtocol(_ subject: URLProtocol, didLoad _: Data) {
        record(.data, subject: subject)
    }

    func urlProtocolDidFinishLoading(_ subject: URLProtocol) {
        record(.finished, subject: subject)
    }

    private func record(_ event: Event, subject: URLProtocol) {
        lock.withLock { recordedEvents.append(event) }
        if stopAfter == event { subject.stopLoading() }
    }

    func urlProtocol(_: URLProtocol, didFailWithError _: Error) {}
    func urlProtocol(_: URLProtocol, wasRedirectedTo _: URLRequest, redirectResponse _: URLResponse) {}
    func urlProtocol(_: URLProtocol, cachedResponseIsValid _: CachedURLResponse) {}
    func urlProtocol(_: URLProtocol, didReceive _: URLAuthenticationChallenge) {}
    func urlProtocol(_: URLProtocol, didCancel _: URLAuthenticationChallenge) {}
}
