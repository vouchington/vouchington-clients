import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class AndroidSessionRestoreRetryTests: XCTestCase {
    func testTransientRestoreRetainsRetryUntilSuccessOrUnauthorized() async throws {
        let path = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [path])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(path, root: root))
        for finalStatus in [200, 401] {
            let host = "restore-\(UUID().uuidString.lowercased()).example.com"
            defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
            let url = try XCTUnwrap(URL(string: "https://\(host)"))
            let storage = AndroidSessionCookieStorage(secrets: CookieReadFailureSecrets(), baseURL: url)
            try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
                .name: "st", .value: "saved-session", .domain: host, .path: "/", .secure: "TRUE"
            ])))
            let account = AndroidAccount(
                config: AppConfig(baseURL: url),
                cookieStorage: storage,
                protocolClasses: [HeldAuthenticationURLProtocol.self]
            )
            await finishIdentityRequest(host: host, status: 503, body: Data("{}".utf8)) {
                await account.restore()
            }
            XCTAssertTrue(account.isRestoring)
            XCTAssertNotNil(account.sessionRestoreError)
            XCTAssertFalse(account.session.isSignedIn)
            XCTAssertEqual(storage.cookies?.map(\.value), ["saved-session"])
            await finishIdentityRequest(host: host, status: finalStatus, body: identity) {
                await account.retryCookiePersistenceOrRestoration()
            }
            XCTAssertFalse(account.isRestoring)
            XCTAssertNil(account.sessionRestoreError)
            XCTAssertEqual(account.session.isSignedIn, finalStatus == 200)
            XCTAssertEqual(storage.cookies?.map(\.value), ["saved-session"])
        }
    }

    private func finishIdentityRequest(
        host: String, status: Int, body: Data,
        operation: @escaping @MainActor () async -> Void
    ) async {
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
        let request = Task { await operation() }
        await fulfillment(of: [started], timeout: 3)
        let released = HeldAuthenticationURLProtocol.respond(host: host, statusCode: status, body: body)
        if !released { request.cancel() }
        await request.value
        XCTAssertTrue(released)
    }
}
