import Foundation
import XCTest

/// Regression test for the intermittent CI `test-ui` SIGSEGV: a delayed `CannedFeedURLProtocol`
/// delivery must not call back into its `URLProtocolClient` once `stopLoading()` has run, because
/// by then the client's owning `URLSession`/`APIClient` may already be torn down. Fails against the
/// pre-fix empty `stopLoading()` (all three callback counts land at 1) and passes once `deliver`
/// rechecks stopped state after its sleep.
final class CannedFeedURLProtocolStopLoadingTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    override func tearDown() {
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    func testConcurrentResetWaitsForCaptureLock() throws {
        let path = "/canned-feed-url-protocol-concurrent-reset-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        let client = RecordingURLProtocolClient()
        let protocolInstance = CannedFeedURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )
        let captureLocked = DispatchSemaphore(value: 0)
        let captureMayProceed = DispatchSemaphore(value: 0)
        let resetWillAcquireLock = DispatchSemaphore(value: 0)
        let resetFinished = DispatchSemaphore(value: 0)
        let startLoadingFinished = DispatchSemaphore(value: 0)
        protocolInstance.onWillCaptureRequest = {
            captureLocked.signal()
            captureMayProceed.wait()
        }

        DispatchQueue.global().async {
            protocolInstance.startLoading()
            startLoadingFinished.signal()
        }
        XCTAssertEqual(captureLocked.wait(timeout: .now() + 2), .success)

        DispatchQueue.global().async {
            CannedFeedURLProtocol.resetCapturedRequests {
                resetWillAcquireLock.signal()
            }
            resetFinished.signal()
        }
        XCTAssertEqual(resetWillAcquireLock.wait(timeout: .now() + 2), .success)
        XCTAssertEqual(resetFinished.wait(timeout: .now() + 0.05), .timedOut)

        captureMayProceed.signal()
        XCTAssertEqual(resetFinished.wait(timeout: .now() + 2), .success)
        XCTAssertEqual(startLoadingFinished.wait(timeout: .now() + 2), .success)
        XCTAssertTrue(CannedFeedURLProtocol.capturedRequests.isEmpty)
    }

    func testStoppedProtocolDeliversNoCallbacksAfterDelayedResponse() throws {
        let path = "/canned-feed-url-protocol-stop-loading-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        CannedFeedURLProtocol.queuedHandlers[path] = [(Data("{}".utf8), 200, 0.05)]

        let client = RecordingURLProtocolClient()
        let protocolInstance = CannedFeedURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        // Stop before the delayed delivery ever runs, mirroring a cancelled URLSessionTask racing
        // a still-sleeping delivery thread. `deliver(...)` must recheck stopped state after its
        // sleep and refuse to call back into a client that may already be gone.
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
        let path = "/canned-feed-url-protocol-not-stopped-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        CannedFeedURLProtocol.queuedHandlers[path] = [(Data("{}".utf8), 200, 0)]

        let client = RecordingURLProtocolClient()
        let protocolInstance = CannedFeedURLProtocol(
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
        let path = "/canned-feed-url-protocol-concurrent-stop-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        CannedFeedURLProtocol.queuedHandlers[path] = [(Data("{}".utf8), 200, 0.05)]

        let client = RecordingURLProtocolClient()
        let protocolInstance = CannedFeedURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        // startLoading() sleeps synchronously for the response delay, so it must run off this
        // thread for stopLoading() to have a chance to race it — this exercises the actual
        // production race (a cancel arriving while `deliver` is mid-sleep), not just the
        // already-stopped-before-start case above. `onWillSleepForDelivery` blocks the worker on
        // `mayProceed` until this test has called `stopLoading()` and signaled it, so
        // `stopLoading()` always happens-before the post-sleep stopped recheck — no scheduler-
        // dependent window, unlike racing a fixed timer that can fire late under CI runner load.
        let enteredDelayWindow = expectation(description: "deliver entered its delay sleep")
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

    func testStartLoadingDeliversFailureForCannedError() throws {
        let path = "/canned-feed-url-protocol-error-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        CannedFeedURLProtocol.errors[path] = URLError(.badServerResponse)

        let client = RecordingURLProtocolClient()
        let protocolInstance = CannedFeedURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        protocolInstance.startLoading()

        XCTAssertEqual(client.didFailWithErrorCount, 1)
        XCTAssertEqual(client.didReceiveResponseCount, 0)
        XCTAssertEqual(client.didLoadDataCount, 0)
        XCTAssertEqual(client.didFinishLoadingCount, 0)
    }

    func testStoppedProtocolDeliversNoFailureForCannedError() throws {
        let path = "/canned-feed-url-protocol-stopped-error-test"
        let url = try XCTUnwrap(URL(string: "http://localhost:2999\(path)"))
        CannedFeedURLProtocol.errors[path] = URLError(.badServerResponse)

        let client = RecordingURLProtocolClient()
        let protocolInstance = CannedFeedURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        protocolInstance.stopLoading()
        protocolInstance.startLoading()

        XCTAssertEqual(client.didFailWithErrorCount, 0)
    }
}

private final class RecordingURLProtocolClient: NSObject, URLProtocolClient {
    private(set) var didReceiveResponseCount = 0
    private(set) var didLoadDataCount = 0
    private(set) var didFinishLoadingCount = 0
    private(set) var didFailWithErrorCount = 0

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

    func urlProtocol(_: URLProtocol, didFailWithError _: Error) {
        didFailWithErrorCount += 1
    }

    func urlProtocol(
        _: URLProtocol,
        wasRedirectedTo _: URLRequest,
        redirectResponse _: URLResponse
    ) {}

    func urlProtocol(_: URLProtocol, cachedResponseIsValid _: CachedURLResponse) {}

    func urlProtocol(_: URLProtocol, didReceive _: URLAuthenticationChallenge) {}

    func urlProtocol(_: URLProtocol, didCancel _: URLAuthenticationChallenge) {}
}
