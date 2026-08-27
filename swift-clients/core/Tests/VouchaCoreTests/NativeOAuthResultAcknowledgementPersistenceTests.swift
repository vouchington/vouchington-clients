import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

@MainActor
final class NativeOAuthResultAcknowledgementPersistenceTests: XCTestCase {
    func testDeletionFailureRetainsResultWithoutPublishingAndCanRetry() async throws {
        let persistence = DeletionFailingOAuthSecureState()
        let store = NativeOAuthAuthorizationStore(secureState: persistence)
        try store.record(.authenticated(provider: .github))
        let coordinator = try makeCoordinator(store: store)
        persistence.failNextDelete = true
        var changeCount = 0
        let observer = NotificationCenter.default.addObserver(
            forName: .nativeOAuthAuthorizationDidChange,
            object: nil,
            queue: nil
        ) { _ in
            changeCount += 1
        }
        defer { NotificationCenter.default.removeObserver(observer) }

        coordinator.acknowledgeResult()

        XCTAssertEqual(coordinator.result, .authenticated(provider: .github))
        XCTAssertEqual(
            coordinator.errorMessage,
            NativeOAuthSecureStateError.unavailable.localizedDescription
        )
        XCTAssertTrue(coordinator.canRecoverFailedFinalization)
        XCTAssertEqual(changeCount, 0)

        await coordinator.retryFailedFinalization()

        XCTAssertNil(coordinator.result)
        XCTAssertNil(coordinator.errorMessage)
        XCTAssertFalse(coordinator.canRecoverFailedFinalization)
        XCTAssertEqual(changeCount, 1)
    }

    private func makeCoordinator(
        store: NativeOAuthAuthorizationStore
    ) throws -> NativeOAuthAuthorizationCoordinator {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test"
            ),
            cookieStorage: HTTPCookieStorage(),
            bootstrapSession: false
        )
        return NativeOAuthAuthorizationCoordinator(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage()),
            store: store
        )
    }
}

private final class DeletionFailingOAuthSecureState:
    NativeOAuthSecureStatePersisting,
    @unchecked Sendable {
    private let lock = NSLock()
    private var data: Data?
    var failNextDelete = false

    func read() -> Data? {
        lock.withLock { data }
    }

    func write(_ data: Data) -> Bool {
        lock.withLock { self.data = data }
        return true
    }

    func delete() throws {
        try lock.withLock {
            if failNextDelete {
                failNextDelete = false
                throw NativeOAuthSecureStateError.unavailable
            } else {
                data = nil
            }
        }
    }
}
