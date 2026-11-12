import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
import XCTest

final class AndroidSessionFixtureTests: XCTestCase {
    func testFixtureRejectsMissingOrMismatchedCredentialsInsteadOfInventingAuthenticatedSession() async throws {
        for (tokens, cookie) in [
            ([:], nil),
            (["st": "expected"], nil),
            (["st": "other"], "st=expected"),
            (["dt": "device", "st": "session"], "st=session")
        ] as [([String: String], String?)] {
            let (data, status) = try await request(tokens: tokens, cookie: cookie)
            XCTAssertEqual(status, 401)
            XCTAssertEqual(try JSONDecoder().decode([String: String].self, from: data)["code"], "AUTH_REQUIRED")
        }
    }

    func testFixtureReturnsPinnedRotationOnlyForTheCredentialsActuallySentByTheClient() async throws {
        let (data, status) = try await request(
            tokens: ["dt": "device", "st": "session"], cookie: "dt=device; st=session"
        )
        XCTAssertEqual(status, 200)
        XCTAssertEqual(data, try androidSessionRefreshFixture())
    }

    private func request(tokens: [String: String], cookie: String?) async throws -> (Data, Int) {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [AndroidAuthURLProtocol.self]
        configuration.httpShouldSetCookies = false
        let session = URLSession(configuration: configuration)
        defer { session.invalidateAndCancel() }
        var request = try URLRequest(url: XCTUnwrap(URL(string: "https://fixture-session.voucha.ai/api/v1/session")))
        request.httpMethod = "PATCH"
        request.httpBody = try JSONEncoder().encode(tokens)
        request.setValue(cookie, forHTTPHeaderField: "Cookie")
        let (data, response) = try await session.data(for: request)
        return try (data, XCTUnwrap(response as? HTTPURLResponse).statusCode)
    }
}
