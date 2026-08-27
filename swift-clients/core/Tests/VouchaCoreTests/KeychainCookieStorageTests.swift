#if canImport(Security)
    import Foundation
    import Security
    @testable import VouchaAuth
    import XCTest

    final class KeychainCookieStorageTests: XCTestCase {
        private final class PersistenceStore {
            var cookies: [HTTPCookie]
            var saveCount = 0

            init(cookies: [HTTPCookie] = []) {
                self.cookies = cookies
            }

            func storage() -> KeychainCookieStorage {
                KeychainCookieStorage(
                    loadPersistedCookies: { self.cookies },
                    savePersistedCookies: {
                        self.cookies = $0
                        self.saveCount += 1
                    },
                    deletePersistedCookies: { self.cookies = [] }
                )
            }
        }

        private final class KeychainBackendStore {
            var data: Data?
            var queries: [NSDictionary] = []

            func backend() -> KeychainCookiePersistence.Backend {
                KeychainCookiePersistence.Backend(
                    copyMatching: { query, result in
                        self.queries.append(query as NSDictionary)
                        guard let data = self.data else { return errSecItemNotFound }
                        result?.pointee = data as CFData
                        return errSecSuccess
                    },
                    update: { query, attributes in
                        self.queries.append(query as NSDictionary)
                        guard self.data != nil else { return errSecItemNotFound }
                        self.data = (attributes as NSDictionary)[kSecValueData] as? Data
                        return errSecSuccess
                    },
                    add: { query, _ in
                        let dictionary = query as NSDictionary
                        self.queries.append(dictionary)
                        self.data = dictionary[kSecValueData] as? Data
                        return errSecSuccess
                    },
                    delete: { query in
                        self.queries.append(query as NSDictionary)
                        self.data = nil
                        return errSecSuccess
                    }
                )
            }
        }

        private var storage: KeychainCookieStorage!
        private var persistence: PersistenceStore!

        override func setUp() {
            super.setUp()
            persistence = PersistenceStore()
            storage = persistence.storage()
        }

        override func tearDown() {
            storage.clearAll()
            super.tearDown()
        }

        private func makeCookie(
            name: String,
            value: String,
            domain: String,
            expiresDate: Date? = nil
        ) -> HTTPCookie? {
            var properties: [HTTPCookiePropertyKey: Any] = [
                .name: name,
                .value: value,
                .domain: domain,
                .path: "/"
            ]
            if let expiresDate {
                properties[.expires] = expiresDate
            }
            return HTTPCookie(properties: properties)
        }

        func testExpiredCookiePassedToSetCookieDeletesStoredCookie() throws {
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            let live = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok123",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let expired = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok123",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 1)
                )
            )

            storage.setCookie(live)
            storage.setCookie(expired)

            XCTAssertNil(storage.cookies?.first(where: { $0.name == "dt" }))
            XCTAssertTrue(storage.cookies(for: url)?.isEmpty ?? true)

            let reloaded = persistence.storage()
            XCTAssertNil(reloaded.cookies?.first(where: { $0.name == "dt" }))
        }

        func testExpiredCookiePassedToSetCookiesDeletesStoredCookie() throws {
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            let live = try XCTUnwrap(
                makeCookie(
                    name: "st",
                    value: "sess456",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let expired = try XCTUnwrap(
                makeCookie(
                    name: "st",
                    value: "sess456",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 1)
                )
            )

            storage.setCookies([live], for: url, mainDocumentURL: nil)
            storage.setCookies([expired], for: url, mainDocumentURL: nil)

            XCTAssertTrue(storage.cookies(for: url)?.isEmpty ?? true)

            let reloaded = persistence.storage()
            XCTAssertTrue(reloaded.cookies(for: url)?.isEmpty ?? true)
        }

        func testExpiredCookiesArePrunedFromReadsAndPersisted() throws {
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            let live = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok123",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let expired = try XCTUnwrap(
                makeCookie(
                    name: "st",
                    value: "sess456",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 1)
                )
            )

            persistence.cookies = [live, expired]

            let reloaded = persistence.storage()
            XCTAssertEqual(reloaded.cookies?.map(\.name).sorted(), ["dt"])
            XCTAssertEqual(reloaded.cookies(for: url)?.map(\.name).sorted(), ["dt"])

            let pruned = persistence.storage()
            XCTAssertEqual(pruned.cookies?.map(\.name).sorted(), ["dt"])
        }

        func testMutationsPersistOnceAfterPruningExpiredCookies() throws {
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            let expired = try XCTUnwrap(
                makeCookie(
                    name: "stale",
                    value: "old",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 1)
                )
            )
            let live = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok123",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )

            persistence = PersistenceStore(cookies: [expired])
            storage = persistence.storage()
            storage.setCookie(live)
            XCTAssertEqual(persistence.saveCount, 1)

            persistence = PersistenceStore(cookies: [expired])
            storage = persistence.storage()
            storage.setCookies([live], for: url, mainDocumentURL: nil)
            XCTAssertEqual(persistence.saveCount, 1)

            persistence = PersistenceStore(cookies: [expired, live])
            storage = persistence.storage()
            storage.deleteCookie(live)
            XCTAssertEqual(persistence.saveCount, 1)
        }

        func testPublicInitializerUsesKeychainPersistenceBackend() throws {
            let serviceName = "ai.voucha.test.\(UUID().uuidString)"
            let accessGroup = "ai.voucha.test.group"
            let backendStore = KeychainBackendStore()
            let backend = backendStore.backend()

            let keychainStorage = KeychainCookieStorage(
                serviceName: serviceName,
                accessGroup: accessGroup,
                backend: backend
            )
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            let cookie = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok123",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )

            keychainStorage.setCookie(cookie)
            let reloaded = KeychainCookieStorage(
                serviceName: serviceName,
                accessGroup: accessGroup,
                backend: backend
            )

            XCTAssertEqual(reloaded.cookies(for: url)?.first(where: { $0.name == "dt" })?.value, "tok123")
            XCTAssertTrue(
                backendStore.queries.contains {
                    $0[kSecAttrService] as? String == serviceName
                        && $0[kSecAttrAccount] as? String == "\(serviceName).cookies"
                        && $0[kSecAttrAccessGroup] as? String == accessGroup
                }
            )

            keychainStorage.clearAll()
            XCTAssertNil(backendStore.data)
        }

        func testKeychainPersistenceFiltersExpiredRawMaximumAgeCookiesBeforeRebuilding() throws {
            let serviceName = "ai.voucha.test.\(UUID().uuidString)"
            let accessGroup = "ai.voucha.test.group"
            let backendStore = KeychainBackendStore()
            let created = Date().addingTimeInterval(-120).timeIntervalSinceReferenceDate
            backendStore.data = try PropertyListSerialization.data(
                fromPropertyList: [
                    [
                        .name: "dt",
                        .value: "tok123",
                        .domain: "api.voucha.ai",
                        .path: "/",
                        .maximumAge: "60",
                        KeychainCookieExpiration.createdPropertyKey: created
                    ]
                ],
                format: .binary,
                options: 0
            )

            let reloaded = KeychainCookieStorage(
                serviceName: serviceName,
                accessGroup: accessGroup,
                backend: backendStore.backend()
            )

            XCTAssertTrue(reloaded.cookies?.isEmpty ?? false)
            let persisted = try XCTUnwrap(
                PropertyListSerialization.propertyList(from: XCTUnwrap(backendStore.data), format: nil)
                    as? [[HTTPCookiePropertyKey: Any]]
            )
            XCTAssertTrue(persisted.isEmpty)
        }

        func testKeychainPersistenceDropsLegacyRawMaximumAgeCookiesWithoutCreationTime() throws {
            let serviceName = "ai.voucha.test.\(UUID().uuidString)"
            let accessGroup = "ai.voucha.test.group"
            let backendStore = KeychainBackendStore()
            let raw: [[HTTPCookiePropertyKey: Any]] = [
                [
                    .name: "dt",
                    .value: "tok123",
                    .domain: "api.voucha.ai",
                    .path: "/",
                    .maximumAge: "60"
                ]
            ]
            backendStore.data = try PropertyListSerialization.data(
                fromPropertyList: raw,
                format: .binary,
                options: 0
            )

            let reloaded = KeychainCookieStorage(
                serviceName: serviceName,
                accessGroup: accessGroup,
                backend: backendStore.backend()
            )

            XCTAssertTrue(reloaded.cookies?.isEmpty ?? false)
        }

        func testSetAndGetCookie() throws {
            let cookie = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok123",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            storage.setCookies([cookie], for: url, mainDocumentURL: nil)

            let fetched = storage.cookies(for: url)
            XCTAssertEqual(fetched?.first(where: { $0.name == "dt" })?.value, "tok123")
        }

        func testDeleteCookie() throws {
            let cookie = try XCTUnwrap(
                makeCookie(
                    name: "st",
                    value: "sess456",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            storage.setCookies([cookie], for: url, mainDocumentURL: nil)
            storage.deleteCookie(cookie)

            let fetched = storage.cookies(for: url)
            XCTAssertNil(fetched?.first(where: { $0.name == "st" }))
        }

        func testClearAllRemovesAllCookies() throws {
            let dt = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "a",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let st = try XCTUnwrap(
                makeCookie(
                    name: "st",
                    value: "b",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
            storage.setCookies([dt, st], for: url, mainDocumentURL: nil)
            storage.clearAll()
            XCTAssertTrue(storage.cookies(for: url)?.isEmpty ?? true)
        }

        func testCookieIsolatedByDomain() throws {
            let cookie = try XCTUnwrap(
                makeCookie(
                    name: "dt",
                    value: "tok",
                    domain: "api.voucha.ai",
                    expiresDate: Date(timeIntervalSince1970: 4_102_444_800)
                )
            )
            try storage.setCookies([cookie], for: XCTUnwrap(URL(string: "https://api.voucha.ai")), mainDocumentURL: nil)
            XCTAssertTrue(
                try storage.cookies(for: XCTUnwrap(URL(string: "https://evil.example.com")))?.isEmpty ?? true
            )
        }
    }
#endif
