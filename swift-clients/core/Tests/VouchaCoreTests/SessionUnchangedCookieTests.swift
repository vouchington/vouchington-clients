import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class SessionUnchangedCookieTests: XCTestCase {
    private struct Empty: Decodable {}

    func testOrdinaryResponseWithoutSetCookieDoesNotWriteOrInvalidateLiveCredentials() async throws {
        try await assertUnchangedResponse(headers: [:])
    }

    func testIdenticalSetCookieDoesNotWriteOrInvalidateLiveCredentials() async throws {
        try await assertUnchangedResponse(headers: ["Set-Cookie": "st=saved; Path=/"])
    }

    private func assertUnchangedResponse(headers: [String: String]) async throws {
        let host = "unchanged-\(UUID().uuidString.lowercased()).test"
        let url = try XCTUnwrap(URL(string: "http://\(host)"))
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        try jar.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .domain: host, .path: "/", .name: "st", .value: "saved"
        ])))
        let transport = SessionRecoveryTransport()
        transport.handler = { _, _ in .init(status: 200, body: Data("{}".utf8), headers: headers) }
        SessionRecoveryProtocol.register(transport, host: host)
        defer { SessionRecoveryProtocol.remove(host) }
        let client = APIClient(
            config: AppConfig(baseURL: url), cookieStorage: jar,
            protocolClasses: [SessionRecoveryProtocol.self], bootstrapSession: false,
            persistCookiesChecked: {
                XCTFail("An unchanged response must never attempt durable replacement")
                throw CocoaError(.fileWriteNoPermission)
            }
        )
        let epoch = client.sessionEpoch
        let _: Empty = try await client.send(Endpoint(path: "/ordinary-read"))
        XCTAssertEqual(client.sessionEpoch, epoch)
        XCTAssertEqual(jar.cookies?.map(\.value), ["saved"])
        XCTAssertEqual(transport.captured.count, 1)
    }

    func testChangedPairAttributesAndExpiredDeletionStillPersist() throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        var writes = 0
        let persist = { writes += 1 }
        func cookie(_ name: String, expires: Date? = nil) throws -> HTTPCookie {
            var properties: [HTTPCookiePropertyKey: Any] = [
                .domain: "example.test", .path: "/", .name: name, .value: "token"
            ]
            if let expires { properties[.expires] = expires }
            return try XCTUnwrap(HTTPCookie(properties: properties))
        }
        try coordinator.commit([cookie("dt"), cookie("st")], stamp: coordinator.stamp, persist: persist, rotated: true)
        XCTAssertEqual(writes, 1)
        try coordinator.commit(
            [cookie("st", expires: Date().addingTimeInterval(600))],
            stamp: coordinator.stamp,
            persist: persist
        )
        XCTAssertEqual(writes, 2, "Expiry changes are credential changes even when the token value is identical")
        try coordinator.commit(
            [cookie("st", expires: Date(timeIntervalSince1970: 1))],
            stamp: coordinator.stamp,
            persist: persist
        )
        XCTAssertEqual(writes, 3)
        XCTAssertEqual(jar.cookies?.map(\.name), ["dt"])
    }

    func testUnchangedRotationStillAdvancesRevisionAndRejectsStaleEpoch() throws {
        let jar = try XCTUnwrap(URLSessionConfiguration.ephemeral.httpCookieStorage)
        let cookie = try XCTUnwrap(HTTPCookie(properties: [
            .domain: "example.test", .path: "/", .name: "st", .value: "saved"
        ]))
        jar.setCookie(cookie)
        let coordinator = SessionCookieCoordinator.shared(for: jar)
        let original = coordinator.stamp
        try coordinator.commit([cookie], stamp: original, persist: {
            XCTFail("Identical snapshot must not be persisted")
        }, rotated: true, authenticated: true)
        XCTAssertEqual(coordinator.stamp.epoch, original.epoch)
        XCTAssertEqual(coordinator.stamp.revision, original.revision + 1)
        XCTAssertTrue(coordinator.hasConfirmedIdentity)
        coordinator.invalidate()
        XCTAssertThrowsError(try coordinator.commit([], stamp: original, persist: nil))
        XCTAssertThrowsError(try coordinator.commit([cookie], stamp: original, persist: nil))
    }
}
