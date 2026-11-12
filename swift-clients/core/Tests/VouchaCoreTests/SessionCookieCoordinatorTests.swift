import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import XCTest

final class SessionCookieCoordinatorTests: XCTestCase {
    func testJarSharedFlightRunsExactlyOnceAndOldRevisionDoesNotRotateAgain() async throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        XCTAssertTrue(coordinator === SessionCookieCoordinator.shared(for: jar))
        let gate = RotationGate()
        let url = try XCTUnwrap(URL(string: "https://example.test"))
        let result = SessionRotationResult(
            data: Data(),
            response: URLResponse(url: url, mimeType: nil, expectedContentLength: 0, textEncodingName: nil)
        )
        let stamp = coordinator.stamp
        let operation: @Sendable (SessionCookieCoordinator.Stamp) async throws -> SessionRotationResult = { started in
            await gate.wait()
            try coordinator.commit([], stamp: started, persist: nil, rotated: true)
            return result
        }
        let first = try XCTUnwrap(coordinator.rotationTask(since: stamp, operation: operation))
        let second = try XCTUnwrap(coordinator.rotationTask(since: stamp, operation: operation))
        XCTAssertEqual(first, second)
        await gate.open()
        _ = try await first.value
        _ = try await second.value
        let stale = try await coordinator.rotate(since: stamp) { _ in
            XCTFail("Already rotated credentials must not be submitted again")
            return result
        }
        XCTAssertNil(stale)
        try await coordinator.awaitRotation()
    }

    func testCookieCommitRejectsOldEpochAndIgnoresOldRevision() throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let stamp = coordinator.stamp
        try coordinator.commit([cookie("fresh")], stamp: stamp, persist: nil, rotated: true)
        try coordinator.commit([cookie("late")], stamp: stamp, persist: nil)
        XCTAssertEqual(jar.cookies?.map(\.value), ["fresh"])
        coordinator.fenceRequests()
        XCTAssertThrowsError(try coordinator.commit([cookie("late")], stamp: stamp, persist: nil))
    }

    func testPrepareIncludesOnlyOriginCookiesAndDisablesAutomaticHandling() throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        jar.setCookie(cookie("saved"))
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let origin = try XCTUnwrap(URL(string: "https://example.test"))
        let (same, _) = try coordinator.prepare(URLRequest(url: origin), origin: origin)
        XCTAssertEqual(same.value(forHTTPHeaderField: "Cookie"), "st=saved")
        XCTAssertFalse(same.httpShouldHandleCookies)
        let (other, _) = try coordinator.prepare(
            URLRequest(url: XCTUnwrap(URL(string: "https://other.test"))),
            origin: origin
        )
        XCTAssertNil(other.value(forHTTPHeaderField: "Cookie"))
    }

    func testPersistenceFailureFencesAndDiscardsUncommittedCookies() throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        jar.setCookie(cookie("saved"))
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let stamp = coordinator.stamp
        XCTAssertThrowsError(try coordinator.commit([cookie("rotated")], stamp: stamp, persist: {
            throw CocoaError(.fileWriteNoPermission)
        }, rotated: true))
        XCTAssertTrue(jar.cookies?.isEmpty == true)
        XCTAssertThrowsError(try coordinator.check(stamp))
        try coordinator.clear(persist: {})
    }

    func testAuthenticationCookieCommitFencesPriorAccountResponsesAtomically() throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        jar.setCookie(cookie("old-account"))
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let previousAccount = coordinator.stamp
        try coordinator.commit([cookie("new-account")], stamp: previousAccount, persist: {})
        XCTAssertEqual(jar.cookies?.first?.value, "new-account")
        XCTAssertThrowsError(try coordinator.commit([cookie("late-old-account")], stamp: previousAccount, persist: nil))
        XCTAssertThrowsError(try coordinator.clear(ifCurrent: previousAccount, persist: nil))
        XCTAssertEqual(jar.cookies?.first?.value, "new-account")
    }

    func testLogoutBarrierRejectsRequestsAndRotationUntilReleased() async throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let url = try XCTUnwrap(URL(string: "https://example.test"))
        coordinator.beginSignOut()
        XCTAssertThrowsError(try coordinator.prepare(URLRequest(url: url), origin: url))
        let logout = URLRequest(url: url.appendingPathComponent("api/v1/auth/logout"))
        XCTAssertEqual(try coordinator.prepare(logout, origin: url).0.url, logout.url)
        do {
            _ = try await coordinator.rotate(since: coordinator.stamp) { _ in
                XCTFail("Rotation must not start while logout owns the jar")
                throw CancellationError()
            }
            XCTFail("Expected logout barrier")
        } catch is CancellationError {}
        coordinator.endSignOut()
        XCTAssertEqual(try coordinator.prepare(URLRequest(url: url), origin: url).0.url, url)
    }

    func testOldUnauthorizedRequestJoinsNewerActiveRotation() async throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let old = coordinator.stamp
        try coordinator.commit([cookie("first")], stamp: old, persist: nil, rotated: true)
        let gate = RotationGate()
        let current = coordinator.stamp
        let url = try XCTUnwrap(URL(string: "https://example.test"))
        let secondCookie = cookie("second")
        let operation: @Sendable (SessionCookieCoordinator.Stamp) async throws -> SessionRotationResult = { stamp in
            await gate.wait()
            try coordinator.commit([secondCookie], stamp: stamp, persist: nil, rotated: true)
            return SessionRotationResult(
                data: Data(),
                response: URLResponse(url: url, mimeType: nil, expectedContentLength: 0, textEncodingName: nil)
            )
        }
        let active = try XCTUnwrap(coordinator.rotationTask(since: current, operation: operation))
        let joined = try XCTUnwrap(coordinator.rotationTask(since: old, operation: operation))
        XCTAssertEqual(active, joined)
        await gate.open()
        _ = try await joined.value
        XCTAssertEqual(jar.cookies?.first?.value, "second")
    }

    private func cookie(_ value: String) -> HTTPCookie {
        HTTPCookie(properties: [
            .originURL: URL(string: "https://example.test")!,
            .path: "/",
            .name: "st",
            .value: value
        ])!
    }
}

private actor RotationGate {
    private var opened = false
    private var continuation: CheckedContinuation<Void, Never>?

    func wait() async {
        if opened { return }
        await withCheckedContinuation { continuation = $0 }
    }

    func open() {
        opened = true
        continuation?.resume()
        continuation = nil
    }
}
