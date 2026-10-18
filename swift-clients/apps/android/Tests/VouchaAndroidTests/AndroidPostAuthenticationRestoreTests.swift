import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class AndroidPostAuthenticationRestoreTests: XCTestCase {
    func testSuccessfulOTPAndMFAOfferIdentityRetryWithoutReusingCredential() async throws {
        let path = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [path])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(path, root: root))
        for mfa in [false, true] {
            let host = "post-auth-\(UUID().uuidString.lowercased()).example.com"
            defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
            let account = try makeAccount(host: host)
            let endpoint = mfa ? "/api/v1/auth/mfa/totp/verification" : "/api/v1/auth/email-address/login"
            let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: endpoint)
            let verification = Task { if mfa { await account.verifyMFA() } else { await account.verifyOTP() } }
            await fulfillment(of: [started], timeout: 3)
            let refreshing = HeldAuthenticationURLProtocol.advance(
                host: host, to: "/api/v1/my/identity", statusCode: 200, body: Data("{}".utf8)
            )
            await fulfillment(of: [refreshing], timeout: 3)
            XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 503, body: Data("{}".utf8)))
            await verification.value
            XCTAssertTrue(account.isRestoring)
            XCTAssertNotNil(account.sessionRestoreError)
            XCTAssertNil(account.authError)
            XCTAssertNil(account.loginAttemptID)
            XCTAssertEqual(account.otp, "")
            XCTAssertEqual(account.totp, "")
            XCTAssertEqual(account.cookieStorage.cookies?.map(\.value), ["authenticated-session"])

            let retryStarted = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
            let retry = Task { await account.retryCookiePersistenceOrRestoration() }
            await fulfillment(of: [retryStarted], timeout: 3)
            XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 200, body: identity))
            await retry.value
            XCTAssertTrue(account.session.isSignedIn)
            XCTAssertFalse(account.isRestoring)
            XCTAssertNil(account.sessionRestoreError)
        }
    }

    func testUnauthorizedIdentityRetryStartsFreshOTPFlowAfterSuccessfulVerification() async throws {
        for mfa in [false, true] {
            let host = "post-auth-unauthorized-\(UUID().uuidString.lowercased()).example.com"
            defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
            let account = try makeAccount(host: host)
            account.otpRequested = true
            let endpoint = mfa ? "/api/v1/auth/mfa/totp/verification" : "/api/v1/auth/email-address/login"
            let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: endpoint)
            let verification = Task { if mfa { await account.verifyMFA() } else { await account.verifyOTP() } }
            await fulfillment(of: [started], timeout: 3)
            let refreshing = HeldAuthenticationURLProtocol.advance(
                host: host, to: "/api/v1/my/identity", statusCode: 200, body: Data("{}".utf8)
            )
            await fulfillment(of: [refreshing], timeout: 3)
            XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 503, body: Data("{}".utf8)))
            await verification.value
            XCTAssertNotNil(account.sessionRestoreError)
            let retryStarted = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
            let retry = Task { await account.retryCookiePersistenceOrRestoration() }
            await fulfillment(of: [retryStarted], timeout: 3)
            XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 401, body: Data("{}".utf8)))
            await retry.value
            XCTAssertFalse(account.session.isSignedIn)
            XCTAssertFalse(account.isRestoring)
            XCTAssertFalse(account.otpRequested)
            XCTAssertNil(account.loginAttemptID)
            XCTAssertEqual(account.otp, "")
            XCTAssertEqual(account.totp, "")
            XCTAssertNil(account.authError)
        }
    }

    func testRejectedCredentialDoesNotBecomeSessionRestorationFailure() async throws {
        let host = "rejected-auth-\(UUID().uuidString.lowercased()).example.com"
        defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
        let account = try makeAccount(host: host)
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/auth/email-address/login")
        let verification = Task { await account.verifyOTP() }
        await fulfillment(of: [started], timeout: 3)
        XCTAssertTrue(HeldAuthenticationURLProtocol.respond(host: host, statusCode: 401, body: Data("{}".utf8)))
        await verification.value
        XCTAssertFalse(account.isRestoring)
        XCTAssertNil(account.sessionRestoreError)
        XCTAssertNotNil(account.authError)
    }

    private func makeAccount(host: String) throws -> AndroidAccount {
        let url = try XCTUnwrap(URL(string: "https://\(host)"))
        let storage = AndroidSessionCookieStorage(secrets: CookieReadFailureSecrets(), baseURL: url)
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: "st", .value: "authenticated-session", .domain: host, .path: "/", .secure: "TRUE"
        ])))
        let account = AndroidAccount(
            config: AppConfig(baseURL: url),
            cookieStorage: storage,
            protocolClasses: [HeldAuthenticationURLProtocol.self]
        )
        account.isRestoring = false
        account.email = "person@example.com"
        account.otp = "123456"
        account.totp = "654321"
        account.loginAttemptID = "consumed-attempt"
        return account
    }
}
