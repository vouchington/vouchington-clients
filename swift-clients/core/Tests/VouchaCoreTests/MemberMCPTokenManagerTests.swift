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
            let stored = try await store.load(scope: testMCPScope)
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
            let tokens = try await store.load(scope: testMCPScope)
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
        let stored = try await store.load(scope: testMCPScope)
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
            let stored = try await store.load(scope: testMCPScope)
            XCTAssertNil(stored)
        }
    }

    func testInvalidTokenClearAllowsNewBrowserRedemption() async throws {
        MCPTokenURLProtocol.configure(status: 200, data: Data(
            #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                .utf8
        ))
        let store = BlockingTokenStore(blockSave: false)
        let manager = try MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: makeClient())

        try await manager.clear()
        let access = try await manager.redeem(
            code: "new-code", verifier: "verifier",
            redirectURI: XCTUnwrap(URL(string: "https://example.test/oauth/native/macos/callback"))
        )

        XCTAssertEqual(access, "new-access")
        let saved = try await store.load(scope: testMCPScope)
        XCTAssertEqual(saved?.refreshToken, "new-refresh")
    }

    func testSignOutClearsUnreadablePersistedToken() async throws {
        let store = BlockingTokenStore(blockSave: false)
        await store.setFailLoad(true)
        let manager = try MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: makeClient())

        do {
            try await manager.signOut()
            XCTFail("Unreadable storage must surface after cleanup")
        } catch TokenStoreFailure.unreadable {}

        let clears = await store.clearCount()
        XCTAssertEqual(clears, 1)
    }

    func testNewRedemptionWaitsForPriorRefreshSave() async throws {
        MCPTokenURLProtocol.configure(status: 200, data: Data(
            #"{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                .utf8
        ), redeemData: Data(
            #"{"access_token":"redeemed-access","refresh_token":"redeemed-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                .utf8
        ))
        let store = BlockingTokenStore()
        let manager = try MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: makeClient())
        let refresh = Task { try await manager.refresh() }
        await store.waitForSaveStart()
        let redeem = Task {
            try await manager.redeem(
                code: "new-code", verifier: "verifier",
                redirectURI: URL(string: "https://example.test/oauth/native/macos/callback")!
            )
        }
        do {
            XCTAssertEqual(MCPTokenURLProtocol.requestCount(), 1)
            await store.releaseSave()
            let refreshed = try await refresh.value
            let redeemed = try await redeem.value
            XCTAssertEqual(refreshed, "new-access")
            XCTAssertEqual(redeemed, "redeemed-access")
            XCTAssertEqual(MCPTokenURLProtocol.requestCount(), 2)
            let saved = try await store.load(scope: testMCPScope)
            XCTAssertEqual(saved?.refreshToken, "redeemed-refresh")
        } catch {
            await store.releaseSave()
            _ = try? await refresh.value
            _ = try? await redeem.value
            throw error
        }
    }

    func testLate401WaitsForRedemptionStartedDuringStoreLoad() async throws {
        MCPTokenURLProtocol.configure(status: 200, data: Data(
            #"{"access_token":"redeemed-access","refresh_token":"redeemed-refresh","expires_in":3600,"scope":"mcp.user:read","token_type":"Bearer"}"#
                .utf8
        ))
        let store = BlockingTokenStore(blockLoad: true)
        let manager = try MemberMCPTokenManager(accountId: "member-1", store: store, tokenClient: makeClient())
        let retry = Task { try await manager.refresh(rejectedAccessToken: "old-access") }
        await store.waitForLoadStart()
        let redeem = Task {
            try await manager.redeem(
                code: "new-code", verifier: "verifier",
                redirectURI: URL(string: "https://example.test/oauth/native/macos/callback")!
            )
        }
        do {
            await store.waitForSaveStart()
            await store.releaseLoad()
            await store.releaseSave()
            let redeemedAccess = try await redeem.value
            let retriedAccess = try await retry.value
            XCTAssertEqual(redeemedAccess, "redeemed-access")
            XCTAssertEqual(retriedAccess, "redeemed-access")
            XCTAssertEqual(MCPTokenURLProtocol.requestCount(), 1)
        } catch {
            await store.releaseLoad()
            await store.releaseSave()
            _ = try? await redeem.value
            _ = try? await retry.value
            throw error
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
    private var blockNextSave: Bool
    private var blockNextLoad: Bool
    private var loadStarted = false
    private var loadWaitContinuation: CheckedContinuation<Void, Never>?
    private var loadContinuation: CheckedContinuation<Void, Never>?
    private var saveStarted = false
    private var waitContinuation: CheckedContinuation<Void, Never>?
    private var saveContinuation: CheckedContinuation<Void, Never>?
    private var failLoad = false
    private var clears = 0

    init(blockSave: Bool = true, blockLoad: Bool = false) {
        blockNextSave = blockSave
        blockNextLoad = blockLoad
    }

    func load(scope _: MemberMCPOAuthTokenScope) async throws -> MemberMCPOAuthTokens? {
        if blockNextLoad {
            blockNextLoad = false
            loadStarted = true
            loadWaitContinuation?.resume()
            loadWaitContinuation = nil
            await withCheckedContinuation { continuation in loadContinuation = continuation }
        }
        if failLoad { throw TokenStoreFailure.unreadable }
        return tokens
    }

    func waitForLoadStart() async {
        if loadStarted { return }
        await withCheckedContinuation { continuation in loadWaitContinuation = continuation }
    }

    func releaseLoad() {
        loadContinuation?.resume()
        loadContinuation = nil
    }

    func setFailLoad(_ value: Bool) {
        failLoad = value
    }

    func clearCount() -> Int {
        clears
    }

    func setTokens(_ tokens: MemberMCPOAuthTokens) {
        self.tokens = tokens
    }

    func save(_ tokens: MemberMCPOAuthTokens, scope _: MemberMCPOAuthTokenScope) async throws {
        saveStarted = true
        waitContinuation?.resume()
        waitContinuation = nil
        if blockNextSave {
            blockNextSave = false
            await withCheckedContinuation { continuation in saveContinuation = continuation }
        }
        self.tokens = tokens
    }

    func clear(scope _: MemberMCPOAuthTokenScope) async throws {
        tokens = nil
        clears += 1
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

private enum TokenStoreFailure: Error { case unreadable }

private final class MCPTokenURLProtocol: URLProtocol {
    private static let lock = NSLock()
    private static var status = 200
    private static var data = Data()
    private static var redeemData: Data?
    private static var requests = 0
    private static var capturedBody: Data?

    static func configure(status: Int, data: Data, redeemData: Data? = nil) {
        lock.lock()
        self.status = status
        self.data = data
        self.redeemData = redeemData
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
        let body = Self.capturedBody.flatMap { String(data: $0, encoding: .utf8) } ?? ""
        let data = body.contains("grant_type=authorization_code") ? Self.redeemData ?? Self.data : Self.data
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

private let testMCPScope = MemberMCPOAuthTokenScope(
    accountId: "member-1", issuerIdentifier: "https://example.test",
    resource: URL(string: "https://example.test/api/v1/mcp")!,
    clientId: URL(string: "https://example.test/api/v1/oauth/native-clients/macos")!
)
