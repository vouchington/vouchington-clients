import Foundation
import XCTest

/// Regression test for the use-after-free/TOCTOU class fixed in `CannedFeedURLProtocol.swift`
/// (PR #8349): a delayed `ImageUploadURLProtocol` delivery must not call back into its
/// `URLProtocolClient` once `stopLoading()` has run, because by then the client's owning
/// `URLSession`/`APIClient` may already be torn down. Fails against an empty `stopLoading()` (all
/// three callback counts land at 1) and passes once `startLoading()` rechecks stopped state after
/// its sleep.
final class ImageUploadURLProtocolStopLoadingTests: XCTestCase {
    override func setUp() {
        super.setUp()
        ImageUploadURLProtocol.handlers = [:]
        ImageUploadURLProtocol.queuedHandlers = [:]
        ImageUploadURLProtocol.capturedRequests = []
    }

    override func tearDown() {
        ImageUploadURLProtocol.handlers = [:]
        ImageUploadURLProtocol.queuedHandlers = [:]
        ImageUploadURLProtocol.capturedRequests = []
        super.tearDown()
    }

    func testStoppedProtocolDeliversNoCallbacksAfterDelayedResponse() throws {
        let path = "/image-upload-url-protocol-stop-loading-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        ImageUploadURLProtocol.queuedHandlers[path] = [(Data("{}".utf8), 200, 0.05)]

        let client = RecordingURLProtocolClient()
        let protocolInstance = ImageUploadURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        // Stop before the delayed delivery ever runs, mirroring a cancelled URLSessionTask racing
        // a still-sleeping delivery thread.
        protocolInstance.stopLoading()
        protocolInstance.startLoading()

        XCTAssertEqual(client.didReceiveResponseCount, 0)
        XCTAssertEqual(client.didLoadDataCount, 0)
        XCTAssertEqual(client.didFinishLoadingCount, 0)
    }

    func testRunningProtocolDeliversCallbacksWhenNotStopped() throws {
        // Baseline for the test above: without `stopLoading()`, delivery must actually reach the
        // client. This keeps the zero-callback assertion meaningful — it proves the stop is what
        // suppresses delivery, not that the fixture never calls back at all.
        let path = "/image-upload-url-protocol-not-stopped-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        ImageUploadURLProtocol.queuedHandlers[path] = [(Data("{}".utf8), 200, 0)]

        let client = RecordingURLProtocolClient()
        let protocolInstance = ImageUploadURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        protocolInstance.startLoading()

        XCTAssertEqual(client.didReceiveResponseCount, 1)
        XCTAssertEqual(client.didLoadDataCount, 1)
        XCTAssertEqual(client.didFinishLoadingCount, 1)
    }

    func testConcurrentStopDuringDelayedDeliverySuppressesAllCallbacks() throws {
        let path = "/image-upload-url-protocol-concurrent-stop-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        ImageUploadURLProtocol.queuedHandlers[path] = [(Data("{}".utf8), 200, 0.05)]

        let client = RecordingURLProtocolClient()
        let protocolInstance = ImageUploadURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        // startLoading() sleeps synchronously for the response delay, so it must run off this
        // thread for stopLoading() to have a chance to race it. `onWillSleepForDelivery` blocks the
        // worker on `mayProceed` until this test has called `stopLoading()` and signaled it, so
        // `stopLoading()` always happens-before the post-sleep stopped recheck — no scheduler-
        // dependent window, unlike racing a fixed timer that can fire late under CI runner load.
        let enteredDelayWindow = expectation(description: "startLoading entered its delay sleep")
        let mayProceed = DispatchSemaphore(value: 0)
        protocolInstance.onWillSleepForDelivery = {
            enteredDelayWindow.fulfill()
            mayProceed.wait()
        }

        let startLoadingFinished = expectation(description: "startLoading returns")
        DispatchQueue.global().async {
            protocolInstance.startLoading()
            startLoadingFinished.fulfill()
        }
        wait(for: [enteredDelayWindow], timeout: 2)
        protocolInstance.stopLoading()
        mayProceed.signal()
        wait(for: [startLoadingFinished], timeout: 2)

        XCTAssertEqual(client.didReceiveResponseCount, 0)
        XCTAssertEqual(client.didLoadDataCount, 0)
        XCTAssertEqual(client.didFinishLoadingCount, 0)
    }
}

private final class RecordingURLProtocolClient: NSObject, URLProtocolClient {
    private(set) var didReceiveResponseCount = 0
    private(set) var didLoadDataCount = 0
    private(set) var didFinishLoadingCount = 0

    func urlProtocol(
        _: URLProtocol,
        didReceive _: URLResponse,
        cacheStoragePolicy _: URLCache.StoragePolicy
    ) {
        didReceiveResponseCount += 1
    }

    func urlProtocol(_: URLProtocol, didLoad _: Data) {
        didLoadDataCount += 1
    }

    func urlProtocolDidFinishLoading(_: URLProtocol) {
        didFinishLoadingCount += 1
    }

    func urlProtocol(_: URLProtocol, didFailWithError _: Error) {}

    func urlProtocol(
        _: URLProtocol,
        wasRedirectedTo _: URLRequest,
        redirectResponse _: URLResponse
    ) {}

    func urlProtocol(_: URLProtocol, cachedResponseIsValid _: CachedURLResponse) {}

    func urlProtocol(_: URLProtocol, didReceive _: URLAuthenticationChallenge) {}

    func urlProtocol(_: URLProtocol, didCancel _: URLAuthenticationChallenge) {}
}
