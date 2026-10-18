import Foundation
import Observation
import VouchaCore
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import XCTest

@MainActor
final class AndroidCookiePersistenceFailureTests: XCTestCase {
    func testWriteFailureIsVisibleAndExplicitRetryStoresCurrentCookies() async throws {
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try storage.setCookie(cookie("old-session"))
        await storage.flushPendingPersistence()
        let previousSnapshot = secrets.snapshot()
        let account = AndroidAccount(cookieStorage: storage, protocolClasses: [AndroidAuthURLProtocol.self])
        secrets.setWriteFailure(true)
        try storage.setCookie(cookie("new-session"))

        await account.retryCookiePersistence()

        XCTAssertNotNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertEqual(secrets.snapshot(), previousSnapshot)
        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value), ["old-session"])
        secrets.setWriteFailure(false)

        await account.retryCookiePersistence()

        XCTAssertNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.value), ["new-session"])
        try await storage.clearPersistedCookies()
        let writesAfterRemoval = secrets.writeCount()
        try storage.cookieStorage.setCookie(cookie("late-session"))
        await account.retryCookiePersistence()
        XCTAssertEqual(secrets.writeCount(), writesAfterRemoval)
        XCTAssertNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty ?? true)
    }

    func testAutomaticPersistenceCallbackPublishesFailureOnAccountNotice() async throws {
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let account = AndroidAccount(cookieStorage: storage, protocolClasses: [AndroidAuthURLProtocol.self])
        // A queued initial snapshot must not masquerade as the later failure notification.
        account.cookiePersistenceNotice.accept(storage.persistenceStatus)
        secrets.setWriteFailure(true)
        let failedCookie = try cookie("new-session")

        let published = expectation(description: "Persistence failure notice published")
        withObservationTracking {
            _ = account.cookiePersistenceNotice.errorMessage
        } onChange: {
            published.fulfill()
        }
        storage.setCookie(failedCookie)
        await fulfillment(of: [published], timeout: 2)

        XCTAssertNotNil(account.cookiePersistenceNotice.errorMessage)
        XCTAssertNil(try secrets
            .snapshot()[XCTUnwrap(AndroidSessionCookieNamespace.key(for: AppConfig.shared.baseURL))])
    }

    func testOutOfOrderFailuresCannotReplaceSuccessfulRetryOrDisabledStatus() async throws {
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        let notifications = CookiePersistenceNotifications()
        storage.observePersistence { notifications.append($0) }
        let notice = AndroidCookiePersistenceNotice()
        secrets.setWriteFailure(true)
        try storage.setCookie(cookie("new-session"))
        await storage.flushPendingPersistence()
        let failure = try XCTUnwrap(notifications.snapshot().last)
        XCTAssertNotNil(failure.errorMessage)
        secrets.setWriteFailure(false)
        storage.persistCookies()
        await storage.flushPendingPersistence()
        let success = try XCTUnwrap(notifications.snapshot().last)

        notice.accept(success)
        notice.accept(failure)

        XCTAssertNil(notice.errorMessage)
        secrets.setWriteFailure(true)
        storage.persistCookies()
        await storage.flushPendingPersistence()
        let laterFailure = try XCTUnwrap(notifications.snapshot().last)
        notice.accept(laterFailure)
        XCTAssertNotNil(notice.errorMessage)
        storage.disablePersistenceWrites()
        let disabled = try XCTUnwrap(notifications.snapshot().last)
        notice.accept(disabled)
        notice.accept(laterFailure)
        let writesBeforeLateCallback = secrets.writeCount()
        storage.persistCookies()
        await storage.flushPendingPersistence()

        XCTAssertNil(notice.errorMessage)
        XCTAssertFalse(disabled.enabled)
        XCTAssertEqual(secrets.writeCount(), writesBeforeLateCallback)
        XCTAssertEqual(notifications.snapshot().last?.revision, disabled.revision)
    }

    private func cookie(_ value: String) throws -> HTTPCookie {
        try XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: value, .domain: "voucha.ai", .path: "/", .secure: "TRUE"
        ]))
    }
}

private final class CookiePersistenceNotifications: @unchecked Sendable {
    private let lock = NSLock()
    private var statuses: [AndroidSessionCookieStorage.PersistenceStatus] = []

    func append(_ status: AndroidSessionCookieStorage.PersistenceStatus) {
        lock.lock()
        defer { lock.unlock() }
        statuses.append(status)
    }

    func snapshot() -> [AndroidSessionCookieStorage.PersistenceStatus] {
        lock.lock()
        defer { lock.unlock() }
        return statuses
    }
}

private final class FailingCookieWriteSecrets: AndroidEndpointSecretStoring {
    private let lock = NSLock()
    private var values: [String: String] = [:]
    private var fails = false
    private var writes = 0

    func setWriteFailure(_ fails: Bool) {
        lock.lock()
        defer { lock.unlock() }
        self.fails = fails
    }

    func snapshot() -> [String: String] {
        lock.lock()
        defer { lock.unlock() }
        return values
    }

    func writeCount() -> Int {
        lock.lock()
        defer { lock.unlock() }
        return writes
    }

    func string(forKey key: String) throws -> String? {
        lock.lock()
        defer { lock.unlock() }
        return values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        writes += 1
        if fails { throw URLError(.cannotWriteToFile) }
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        lock.lock()
        defer { lock.unlock() }
        values.removeValue(forKey: key)
    }
}
