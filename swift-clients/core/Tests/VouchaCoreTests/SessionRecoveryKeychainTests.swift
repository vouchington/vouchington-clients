#if canImport(Security)
    import Foundation
    import Security
    @testable import VouchaAPI
    @testable import VouchaAuth
    import XCTest

    final class SessionRecoveryKeychainTests: XCTestCase {
        func testCheckedUpdateAndAddFailuresAreReported() throws {
            for updateStatus in [errSecInteractionNotAllowed, errSecItemNotFound] {
                let backend = KeychainCookiePersistence.Backend(
                    copyMatching: { _, _ in errSecItemNotFound },
                    update: { _, _ in updateStatus },
                    add: { _, _ in errSecAuthFailed },
                    delete: { _ in errSecSuccess }
                )
                let storage = KeychainCookieStorage(serviceName: "test", accessGroup: "group", backend: backend)
                XCTAssertThrowsError(try storage.replaceCookies([cookie("new")])) { error in
                    XCTAssertEqual((error as NSError).domain, NSOSStatusErrorDomain)
                    XCTAssertEqual(
                        (error as NSError).code,
                        Int(updateStatus == errSecItemNotFound ? errSecAuthFailed : updateStatus)
                    )
                }
                XCTAssertTrue(storage.cookies?.isEmpty == true)
            }
        }

        func testDurableSnapshotPrecedesInMemoryPublication() throws {
            var stored: [HTTPCookie] = []
            let storage = KeychainCookieStorage(
                loadPersistedCookies: { [] }, savePersistedCookies: { _ in }, deletePersistedCookies: {},
                commitPersistedCookies: { stored = $0 }
            )
            try storage.replaceCookies([cookie("new")])
            XCTAssertEqual(stored.map(\.value), ["new"])
            XCTAssertEqual(storage.cookies?.map(\.value), ["new"])
            try storage.replaceCookies([])
            XCTAssertTrue(stored.isEmpty)
            XCTAssertTrue(storage.cookies?.isEmpty == true)
        }

        func testSuccessfulMissingItemCreatesAndSubsequentUpdateSucceeds() throws {
            var exists = false
            var writes = 0
            let backend = KeychainCookiePersistence.Backend(
                copyMatching: { _, _ in errSecItemNotFound },
                update: { _, _ in exists ? errSecSuccess : errSecItemNotFound },
                add: { _, _ in exists = true
                    writes += 1
                    return errSecSuccess
                },
                delete: { _ in errSecSuccess }
            )
            let storage = KeychainCookieStorage(serviceName: "test", accessGroup: nil, backend: backend)
            try storage.replaceCookies([cookie("one")])
            try storage.replaceCookies([cookie("two")])
            XCTAssertEqual(writes, 1)
            XCTAssertEqual(storage.cookies?.map(\.value), ["two"])
        }

        func testCoordinatorCommitsAndClearsDurableKeychainSnapshot() throws {
            var persisted: [HTTPCookie] = []
            let storage = KeychainCookieStorage(
                loadPersistedCookies: { [] },
                savePersistedCookies: { _ in },
                deletePersistedCookies: {},
                commitPersistedCookies: { persisted = $0 }
            )
            let coordinator = SessionCookieCoordinator.shared(for: storage)
            try coordinator.commit([cookie("new")], stamp: coordinator.stamp, persist: nil, rotated: true)
            XCTAssertEqual(persisted.map(\.value), ["new"])
            try coordinator.clear(persist: nil)
            XCTAssertTrue(persisted.isEmpty)
            XCTAssertTrue(storage.cookies?.isEmpty == true)
        }

        func testFailedTransactionalClearStillRemovesLiveCookiesAndReportsFailure() throws {
            let storage = KeychainCookieStorage(
                loadPersistedCookies: { [self.cookie("old")] },
                savePersistedCookies: { _ in },
                deletePersistedCookies: {},
                commitPersistedCookies: { _ in
                    throw CocoaError(.fileWriteNoPermission)
                }
            )
            let coordinator = SessionCookieCoordinator.shared(for: storage)
            XCTAssertThrowsError(try coordinator.clear(persist: nil))
            XCTAssertTrue(storage.cookies?.isEmpty == true)
        }

        private func cookie(_ value: String) -> HTTPCookie {
            HTTPCookie(properties: [.domain: "example.test", .path: "/", .name: "st", .value: value])!
        }
    }
#endif
