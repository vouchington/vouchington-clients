import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidCookieCaptureOrderTests: XCTestCase {
    func testConcurrentCallbacksCannotEnqueueAnOlderCapturedSnapshotLast() async throws {
        let oldRecord = try XCTUnwrap(AndroidSessionCookieStorage.CookieRecord(cookie: cookie("old-session")))
        let newRecord = try XCTUnwrap(AndroidSessionCookieStorage.CookieRecord(cookie: cookie("new-session")))
        let firstCaptured = expectation(description: "First snapshot captured")
        let secondStarted = expectation(description: "Second callback started")
        let secondFinished = DispatchSemaphore(value: 0)
        let releaseFirst = DispatchSemaphore(value: 0)
        defer { releaseFirst.signal() }
        let captureOrder = CookieCaptureOrder()
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets, captureRecords: { _ in
            if captureOrder.next() == 1 {
                firstCaptured.fulfill()
                releaseFirst.wait()
                return [oldRecord]
            }
            return [newRecord]
        })

        let first = Task.detached { storage.persistCookies() }
        await fulfillment(of: [firstCaptured], timeout: 2)
        let second = Task.detached {
            secondStarted.fulfill()
            storage.persistCookies()
            secondFinished.signal()
        }
        await fulfillment(of: [secondStarted], timeout: 2)
        XCTAssertEqual(secondFinished.wait(timeout: .now() + 0.1), .timedOut)
        releaseFirst.signal()
        await first.value
        await second.value
        await storage.flushPendingPersistence()

        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value), ["new-session"])
    }

    private func cookie(_ value: String) throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: value, .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ]))
    }
}

private final class CookieCaptureOrder: @unchecked Sendable {
    private let lock = NSLock()
    private var count = 0

    func next() -> Int {
        lock.lock()
        defer { lock.unlock() }
        count += 1
        return count
    }
}
