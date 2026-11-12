import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class AndroidCookieCheckedCommitTests: XCTestCase {
    func testCheckedCommitWaitsForDurableSnapshotAndReportsWriteFailure() throws {
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try storage.cookieStorage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .originURL: AppConfig.shared.baseURL, .path: "/", .name: "st", .value: "rotated-session"
        ])))
        try storage.persistCookiesChecked()
        XCTAssertEqual(AndroidSessionCookieStorage(secrets: secrets).cookies?.first?.value, "rotated-session")
        secrets.setWriteFailure(true)
        XCTAssertThrowsError(try storage.persistCookiesChecked())
        XCTAssertNotNil(storage.persistenceStatus.errorMessage)
    }

    func testAccountRotationFailureNeverPublishesIdentityOrLeavesUsableCookies() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try seedCredentials(storage)
        let account = AndroidAccount(cookieStorage: storage, protocolClasses: [AndroidAuthURLProtocol.self])
        secrets.setWriteFailure(true)
        let restored = await account.session.refresh()
        XCTAssertFalse(restored)
        XCTAssertFalse(account.session.isSignedIn)
        XCTAssertNotNil(account.session.lastRefreshErrorMessage)
        XCTAssertTrue(storage.cookieStorage.cookies?.isEmpty == true)
        XCTAssertEqual(AndroidAuthURLProtocol.requestSnapshot().map { $0.url?.path }, ["/api/v1/session"])
    }

    func testAccountPersistsBothRotatedTokensBeforePublishingIdentity() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let path = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try ContractRoot.url(requiredPaths: [path])
        try AndroidAuthURLProtocol.setIdentity(Data(contentsOf: ContractRoot.requiredURL(path, root: root)))
        let secrets = FailingCookieWriteSecrets()
        let storage = AndroidSessionCookieStorage(secrets: secrets)
        try seedCredentials(storage)
        let account = AndroidAccount(cookieStorage: storage, protocolClasses: [AndroidAuthURLProtocol.self])
        let restored = await account.session.refresh()
        XCTAssertTrue(restored)
        XCTAssertTrue(account.session.isSignedIn)
        XCTAssertEqual(Set(AndroidSessionCookieStorage(secrets: secrets).cookies?.map(\.name) ?? []), ["dt", "st"])
        XCTAssertEqual(
            AndroidAuthURLProtocol.requestSnapshot().map { $0.url?.path },
            ["/api/v1/session", "/api/v1/my/identity"]
        )
    }

    func testLogoutCanClearAfterWriterFenceAndNextAccountCanSignIn() async throws {
        AndroidAuthURLProtocol.resetRequests()
        defer { AndroidAuthURLProtocol.resetRequests() }
        let path = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try ContractRoot.url(requiredPaths: [path])
        try AndroidAuthURLProtocol.setIdentity(Data(contentsOf: ContractRoot.requiredURL(path, root: root)))
        let secrets = FailingCookieWriteSecrets()
        let account = AndroidAccount(
            cookieStorage: AndroidSessionCookieStorage(secrets: secrets),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        try seedCredentials(account.cookieStorage)
        let signedIn = await account.session.refresh()
        XCTAssertTrue(signedIn)
        let signedOut = await account.signOut()
        XCTAssertTrue(signedOut)
        XCTAssertNil(account.session.lastSignOutErrorMessage)
        XCTAssertTrue(AndroidSessionCookieStorage(secrets: secrets).cookies?.isEmpty == true)
        let next = AndroidAccount(
            cookieStorage: AndroidSessionCookieStorage(secrets: secrets),
            protocolClasses: [AndroidAuthURLProtocol.self]
        )
        next.loginAttemptID = "fresh-mfa-attempt"
        next.totp = "654321"
        await next.verifyMFA()
        XCTAssertTrue(next.session.isSignedIn)
    }

    private func seedCredentials(_ storage: AndroidSessionCookieStorage) throws {
        try storage.cookieStorage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .originURL: AppConfig.shared.baseURL, .path: "/", .name: "st", .value: "fixture-previous-session"
        ])))
    }

    func testCheckedCommitRejectsDisabledPersistence() {
        let storage = AndroidSessionCookieStorage(secrets: FailingCookieWriteSecrets())
        storage.disablePersistenceWrites()
        XCTAssertThrowsError(try storage.persistCookiesChecked())
    }
}
