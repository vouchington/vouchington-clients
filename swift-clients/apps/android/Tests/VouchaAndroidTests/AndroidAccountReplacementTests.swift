import Foundation
@testable import VouchaAndroid
import VouchaCore
import VouchaTestSupport
import XCTest

@MainActor
final class AndroidAccountReplacementTests: XCTestCase {
    func testConcurrentReplacementWaitsForSignOutAndReplacesCapturedAccountOnce() async throws {
        let host = "replacement-\(UUID().uuidString.lowercased()).example.com"
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/auth/logout")
        defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
        let original = try makeAccount(host: host)
        let replacement = try makeAccount(host: "new-\(host)")
        let coordinator = AndroidAccountReplacementCoordinator()
        var current = original
        var operations = 0
        var replacements = 0
        let first = Task {
            await coordinator.perform(operation: {
                operations += 1
                return await original.signOut()
            }, isCurrent: { current === original }, replace: {
                replacements += 1
                current = replacement
            })
        }
        await fulfillment(of: [started], timeout: 3)
        await coordinator.perform(operation: {
            operations += 1
            return true
        }, isCurrent: { current === original }, replace: {
            replacements += 1
            current = replacement
        })
        XCTAssertEqual(operations, 1)
        XCTAssertEqual(replacements, 0)
        XCTAssertTrue(coordinator.isReplacing)
        let released = HeldAuthenticationURLProtocol.respond(host: host, statusCode: 200, body: Data("{}".utf8))
        if !released { first.cancel() }
        await first.value
        XCTAssertTrue(released)
        XCTAssertTrue(current === replacement)
        XCTAssertEqual(replacements, 1)
        XCTAssertFalse(original.cookieStorage.persistenceStatus.enabled)
        XCTAssertFalse(coordinator.isReplacing)
    }

    func testStaleCapturedAccountCannotReplaceANewerAccount() async throws {
        let host = "replacement-\(UUID().uuidString.lowercased()).example.com"
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/auth/logout")
        defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
        let original = try makeAccount(host: host)
        let newer = try makeAccount(host: "new-\(host)")
        let coordinator = AndroidAccountReplacementCoordinator()
        var current = original
        var replacements = 0
        let first = Task {
            await coordinator.perform(
                operation: { await original.signOut() },
                isCurrent: { current === original },
                replace: { replacements += 1 }
            )
        }
        await fulfillment(of: [started], timeout: 3)
        current = newer
        let released = HeldAuthenticationURLProtocol.respond(host: host, statusCode: 200, body: Data("{}".utf8))
        if !released { first.cancel() }
        await first.value
        XCTAssertTrue(released)
        XCTAssertTrue(current === newer)
        XCTAssertEqual(replacements, 0)
        XCTAssertFalse(coordinator.isReplacing)
    }

    func testReplacementRemainsSerializedThroughFreshIdentityRestoration() async throws {
        let host = "replacement-\(UUID().uuidString.lowercased()).example.com"
        let started = HeldAuthenticationURLProtocol.expectRequest(host: host, path: "/api/v1/my/identity")
        defer { HeldAuthenticationURLProtocol.removeRequest(host: host) }
        let replacement = try makeAccount(host: host)
        let coordinator = AndroidAccountReplacementCoordinator()
        var operations = 0
        let first = Task {
            await coordinator.perform(operation: {
                operations += 1
                return true
            }, isCurrent: { true }, replace: { await replacement.restore() })
        }
        await fulfillment(of: [started], timeout: 3)
        await coordinator.perform(operation: {
            operations += 1
            return true
        }, isCurrent: { true }, replace: {})
        XCTAssertEqual(operations, 1)
        XCTAssertTrue(coordinator.isReplacing)
        let path = "api-fixtures/v1/responses/swift.my.identity.default.json"
        let root = try FilamentsContractRoot.url(requiredPaths: [path])
        let identity = try Data(contentsOf: FilamentsContractRoot.requiredURL(path, root: root))
        let released = HeldAuthenticationURLProtocol.respond(host: host, statusCode: 200, body: identity)
        if !released { first.cancel() }
        await first.value
        XCTAssertTrue(released)
        XCTAssertTrue(replacement.session.isSignedIn)
        XCTAssertFalse(coordinator.isReplacing)
    }

    func testAlreadyStaleCapturedAccountCannotRunDestructiveOperation() async {
        let coordinator = AndroidAccountReplacementCoordinator()
        var operations = 0
        var replacements = 0
        await coordinator.perform(operation: {
            operations += 1
            return true
        }, isCurrent: { false }, replace: { replacements += 1 })
        XCTAssertEqual(operations, 0)
        XCTAssertEqual(replacements, 0)
        XCTAssertFalse(coordinator.isReplacing)
    }

    private func makeAccount(host: String) throws -> AndroidAccount {
        let url = try XCTUnwrap(URL(string: "https://\(host)"))
        return AndroidAccount(
            config: AppConfig(baseURL: url),
            cookieStorage: AndroidSessionCookieStorage(
                secrets: CookieReadFailureSecrets(),
                baseURL: url
            ),
            protocolClasses: [HeldAuthenticationURLProtocol.self]
        )
    }
}
