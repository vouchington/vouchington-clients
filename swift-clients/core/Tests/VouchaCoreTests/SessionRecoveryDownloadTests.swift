#if canImport(Darwin)
    import Foundation
    @testable import VouchaAPI
    import VouchaCore
    import XCTest

    final class SessionRecoveryDownloadTests: XCTestCase {
        func testDownloadRenewsBeforeReplayingUnconsumedResponse() async throws {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            fixture.transport.handler = { request, count in
                if request.url!.path == "/api/v1/session" { return SessionRecoveryTransport.tokens() }
                return count == 1 ? SessionRecoveryTransport.unauthorized : .init(
                    status: 200,
                    body: Data("export".utf8)
                )
            }
            let file = try await fixture.client.download(for: Endpoint(path: "/export"), filename: "recovery.txt")
            defer { try? FileManager.default.removeItem(at: file) }
            XCTAssertEqual(try Data(contentsOf: file), Data("export".utf8))
            XCTAssertEqual(fixture.transport.captured.map { $0.url!.path }, ["/export", "/api/v1/session", "/export"])
        }

        func testSecondUnauthorizedDownloadClearsAndDoesNotRetryAgain() async throws {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            fixture.transport.handler = { request, _ in
                request.url!.path == "/api/v1/session" ? SessionRecoveryTransport.tokens() : SessionRecoveryTransport
                    .unauthorized
            }
            do {
                _ = try await fixture.client.download(for: Endpoint(path: "/export"), filename: "recovery.txt")
                XCTFail("Expected sign-in requirement")
            } catch VouchaError.unauthorized {}
            XCTAssertEqual(fixture.transport.captured.count, 3)
            XCTAssertTrue(fixture.jar.cookies?.isEmpty == true)
        }

        func testDownloadDestinationFailureIsReturnedInsteadOfAnUnownedFile() async throws {
            let fixture = try setup()
            defer { SessionRecoveryProtocol.remove(fixture.host) }
            fixture.transport.handler = { _, _ in .init(status: 200, body: Data("export".utf8)) }
            do {
                _ = try await fixture.client.download(
                    for: Endpoint(path: "/export"),
                    filename: "missing/subdirectory/export.txt"
                )
                XCTFail("Invalid destination must fail")
            } catch let error as CocoaError { XCTAssertEqual(error.code, .fileNoSuchFile) }
        }

        private func setup() throws -> Setup {
            let host = "download-\(UUID().uuidString.lowercased()).test"
            let jar = URLSessionConfiguration.ephemeral.httpCookieStorage!
            for (name, value) in [("dt", "d-old"), ("st", "s-old")] {
                jar.setCookie(HTTPCookie(properties: [.domain: host, .path: "/", .name: name, .value: value])!)
            }
            let transport = SessionRecoveryTransport()
            SessionRecoveryProtocol.register(transport, host: host)
            let client = APIClient(
                config: AppConfig(baseURL: URL(string: "http://\(host)")!),
                cookieStorage: jar,
                protocolClasses: [SessionRecoveryProtocol.self],
                bootstrapSession: true
            )
            return Setup(client: client, jar: jar, host: host, transport: transport)
        }

        private struct Setup {
            let client: APIClient
            let jar: HTTPCookieStorage
            let host: String
            let transport: SessionRecoveryTransport
        }
    }
#endif
