import Foundation
#if canImport(FoundationNetworking)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaCore
import XCTest

final class FirstPartyRedirectTests: XCTestCase {
    private struct Empty: Decodable {}

    func testBufferedRequestReturnsRedirectWithoutForwardingCredentials() async throws {
        let (server, client) = try setup()
        defer { server.close() }
        async let response: Void = server.respondOnce()
        do {
            let _: Empty = try await client.send(Endpoint(path: "/redirect"))
            XCTFail("Redirect must be surfaced")
        } catch VouchaError.api(statusCode: 302, preconditionCode: nil) {}
        await response
        XCTAssertEqual(server.receivedCookie, "st=private-session")
    }

    func testStreamingHeadersExposeRedirectWithoutFollowingIt() async throws {
        let (server, client) = try setup()
        defer { server.close() }
        async let response: Void = server.respondOnce()
        let result = try await client
            .responseLines(for: URLRequest(url: client.baseURL.appendingPathComponent("redirect")))
        XCTAssertEqual((result.response as? HTTPURLResponse)?.statusCode, 302)
        await response
        XCTAssertEqual(server.receivedCookie, "st=private-session")
    }

    #if canImport(Darwin)
        func testDownloadReturnsRedirectWithoutForwardingCredentials() async throws {
            let (server, client) = try setup()
            defer { server.close() }
            async let response: Void = server.respondOnce()
            do {
                _ = try await client.download(for: Endpoint(path: "/redirect"), filename: "redirect.txt")
                XCTFail("Redirect must be surfaced")
            } catch VouchaError.api(statusCode: 302, preconditionCode: nil) {}
            await response
            XCTAssertEqual(server.receivedCookie, "st=private-session")
        }
    #endif

    private func setup() throws -> (CookieResponseHTTPServer, APIClient) {
        let server =
            try CookieResponseHTTPServer(
                response: "HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:1/exfiltrate\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}"
            )
        let url = URL(string: "http://127.0.0.1:\(server.port)")!
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        jar.setCookie(HTTPCookie(properties: [.originURL: url, .path: "/", .name: "st", .value: "private-session"])!)
        let client = APIClient(
            config: AppConfig(baseURL: url),
            cookieStorage: jar,
            bootstrapSession: false,
            sessionConfiguration: configuration
        )
        return (server, client)
    }
}
