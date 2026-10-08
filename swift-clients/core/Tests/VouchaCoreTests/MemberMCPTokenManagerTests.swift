import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import XCTest

final class MemberMCPTokenManagerTests: XCTestCase {
    override func tearDown() {
        MCPTokenURLProtocol.configure(status: 200, data: Data())
        super.tearDown()
    }

    func testRefreshPersistsRotatedTokenBeforeReturningAccess() async throws {
        let body = Data(
            #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                .utf8
        )
        MCPTokenURLProtocol.configure(status: 200, data: body)
        let store = BlockingTokenStore()
        let client = try makeClient()
        let manager = MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: client)
        let completion = CompletionFlag()
        let task = Task {
            let access = try await manager.refresh()
            await completion.set()
            return access
        }
        do {
            await store.waitForSaveStart()
            let completedBeforeSave = await completion.value()
            XCTAssertFalse(completedBeforeSave)
            XCTAssertEqual(MCPTokenURLProtocol.requestCount(), 1)
            await store.releaseSave()
            let access = try await task.value
            XCTAssertEqual(access, "new-access")
            let completedAfterSave = await completion.value()
            XCTAssertTrue(completedAfterSave)
            let stored = try await store.load(accountId: "member-1")
            XCTAssertEqual(stored?.refreshToken, "new-refresh")
        } catch {
            await store.releaseSave()
            _ = try? await task.value
            throw error
        }
    }

    func testInvalidGrantClearsOldRefreshToken() async throws {
        MCPTokenURLProtocol.configure(status: 400, data: Data(#"{"error":"invalid_grant"}"#.utf8))
        let store = BlockingTokenStore(blockSave: false)
        let manager = try MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: makeClient())
        do {
            _ = try await manager.refresh()
            XCTFail("invalid_grant must require reauthorization")
        } catch MemberMCPTokenManager.Failure.notAuthorized {
            let tokens = try await store.load(accountId: "member-1")
            XCTAssertNil(tokens)
        }
    }

    func testExpiredPersistedAccessRefreshesBeforeMCPUse() async throws {
        MCPTokenURLProtocol.configure(
            status: 200,
            data: Data(
                #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                    .utf8
            )
        )
        let store = BlockingTokenStore(blockSave: false)
        await store.setTokens(MemberMCPOAuthTokens(
            accessToken: "expired-access", refreshToken: "old-refresh", expiresIn: 3_600,
            scope: "mcp.user:read", tokenType: "Bearer",
            acquiredAt: Date(timeIntervalSinceNow: -3_700)
        ))
        let manager = try MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: makeClient())
        let access = try await manager.accessToken()
        XCTAssertEqual(access, "new-access")
        XCTAssertEqual(MCPTokenURLProtocol.requestCount(), 1)
        let stored = try await store.load(accountId: "member-1")
        XCTAssertEqual(stored?.refreshToken, "new-refresh")
        let decoded = try JSONDecoder().decode(
            MemberMCPOAuthTokens.self,
            from: JSONEncoder().encode(XCTUnwrap(stored))
        )
        XCTAssertEqual(decoded.acquiredAt, stored?.acquiredAt)
    }

    func testSignOutAfterHeldRefreshOrRedeemCannotRestoreTokens() async throws {
        let body = Data(
            #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                .utf8
        )
        for redeem in [false, true] {
            MCPTokenURLProtocol.configure(status: 200, data: body)
            let store = BlockingTokenStore()
            let manager = try MemberMCPTokenManager(
                accountId: "member-1", store: store, tokenClient: makeClient()
            )
            let task = Task {
                if redeem {
                    return try await manager.redeem(
                        code: "code-1", verifier: "verifier-1",
                        redirectURI: URL(string: "https://example.test/oauth/native/macos/callback")!
                    )
                }
                return try await manager.refresh()
            }
            await store.waitForSaveStart()
            let signOut = await manager.beginSignOut()
            await store.releaseSave()
            try await signOut.value
            do {
                _ = try await task.value
                XCTFail("An in-flight token cannot authorize after sign-out")
            } catch MemberMCPTokenManager.Failure.notAuthorized {}
            let stored = try await store.load(accountId: "member-1")
            XCTAssertNil(stored)
        }
    }

    func testFormEncodesOpaquePlusAmpersandAndEquals() async throws {
        MCPTokenURLProtocol.configure(
            status: 200,
            data: Data(
                #"{"access_token":"access","refresh_token":"refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                    .utf8
            )
        )
        let client = try makeClient()
        _ = try await client.redeem(
            code: "a+b&=c", verifier: "v+/=",
            redirectURI: XCTUnwrap(URL(string: "https://example.test/oauth/native/macos/callback"))
        )
        let body = try XCTUnwrap(MCPTokenURLProtocol.bodySnapshot())
        let text = try XCTUnwrap(String(data: body, encoding: .utf8))
        XCTAssertTrue(text.contains("code=a%2Bb%26%3Dc"))
        XCTAssertTrue(text.contains("code_verifier=v%2B/%3D"))
        _ = try await client.refresh("a+b&=c")
        let refreshBody = try XCTUnwrap(MCPTokenURLProtocol.bodySnapshot())
        XCTAssertTrue(String(decoding: refreshBody, as: UTF8.self).contains("refresh_token=a%2Bb%26%3Dc"))
    }

    private func makeClient() throws -> MemberMCPOAuthTokenClient {
        try MemberMCPOAuthTokenClient(
            metadata: MemberMCPOAuthMetadata(
                issuerIdentifier: "https://example.test",
                authorizationEndpoint: URL(string: "https://example.test/authorize")!,
                tokenEndpoint: URL(string: "https://example.test/token")!,
                revocationEndpoint: URL(string: "https://example.test/revoke")!
            ),
            clientId: URL(string: "https://example.test/api/v1/oauth/native-clients/macos")!,
            resource: URL(string: "https://example.test/api/v1/mcp")!,
            protocolClasses: [MCPTokenURLProtocol.self]
        )
    }
}

private actor CompletionFlag {
    private var done = false
    func set() {
        done = true
    }

    func value() -> Bool {
        done
    }
}

private actor BlockingTokenStore: MemberMCPOAuthTokenStore {
    private var tokens: MemberMCPOAuthTokens? = MemberMCPOAuthTokens(
        accessToken: "old-access", refreshToken: "old-refresh", expiresIn: 3_600,
        scope: "mcp.user:read", tokenType: "Bearer"
    )
    private let blockSave: Bool
    private var saveStarted = false
    private var waitContinuation: CheckedContinuation<Void, Never>?
    private var saveContinuation: CheckedContinuation<Void, Never>?

    init(blockSave: Bool = true) {
        self.blockSave = blockSave
    }

    func load(accountId _: String) async throws -> MemberMCPOAuthTokens? {
        tokens
    }

    func setTokens(_ tokens: MemberMCPOAuthTokens) {
        self.tokens = tokens
    }

    func save(_ tokens: MemberMCPOAuthTokens, accountId _: String) async throws {
        saveStarted = true
        waitContinuation?.resume()
        waitContinuation = nil
        if blockSave {
            await withCheckedContinuation { continuation in saveContinuation = continuation }
        }
        self.tokens = tokens
    }

    func clear(accountId _: String) async throws {
        tokens = nil
    }

    func waitForSaveStart() async {
        if saveStarted { return }
        await withCheckedContinuation { continuation in waitContinuation = continuation }
    }

    func releaseSave() {
        saveContinuation?.resume()
        saveContinuation = nil
    }
}

private final class MCPTokenURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var status = 200
    private static var data = Data()
    private static var requests = 0
    private static var capturedBody: Data?

    static func configure(status: Int, data: Data) {
        lock.lock()
        self.status = status
        self.data = data
        requests = 0
        capturedBody = nil
        lock.unlock()
    }

    static func bodySnapshot() -> Data? {
        lock.lock()
        defer { lock.unlock() }
        return capturedBody
    }

    static func requestCount() -> Int {
        lock.lock()
        defer { lock.unlock() }
        return requests
    }

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.lock()
        Self.requests += 1
        Self.capturedBody = request.httpBody ?? request.httpBodyStream.map(Self.readBodyStream)
        let status = Self.status
        let data = Self.data
        Self.lock.unlock()
        let response = HTTPURLResponse(
            url: request.url!, statusCode: status, httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    private static func readBodyStream(_ stream: InputStream) -> Data {
        stream.open()
        defer { stream.close() }
        var body = Data()
        var buffer = [UInt8](repeating: 0, count: 4_096)
        while stream.hasBytesAvailable {
            let count = stream.read(&buffer, maxLength: buffer.count)
            if count <= 0 { break }
            body.append(buffer, count: count)
        }
        return body
    }
}
