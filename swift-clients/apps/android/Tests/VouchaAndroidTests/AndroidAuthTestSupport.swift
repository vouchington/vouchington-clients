import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAndroid
import VouchaTestSupport
import XCTest

final class AndroidAuthURLProtocol: URLProtocol {
    private struct ResponseConfiguration {
        let status: Int
        let identity: Data
        let cookie: String?
    }

    private static let lock = NSLock()
    private nonisolated(unsafe) static var requests: [URLRequest] = []
    private nonisolated(unsafe) static var identity = Data()
    private nonisolated(unsafe) static var otpStatusCode = 200
    private nonisolated(unsafe) static var logoutStatusCode = 200
    private nonisolated(unsafe) static var responseCookies: [String: String] = [:]

    static func resetRequests() {
        lock.withLock {
            requests = []
            identity = Data()
            otpStatusCode = 200
            logoutStatusCode = 200
            responseCookies = [:]
        }
    }

    static func setIdentity(_ data: Data) {
        lock.withLock { identity = data }
    }

    static func setOTPStatusCode(_ statusCode: Int) {
        lock.withLock { otpStatusCode = statusCode }
    }

    static func setLogoutStatusCode(_ statusCode: Int) {
        lock.withLock { logoutStatusCode = statusCode }
    }

    static func setResponseCookie(_ cookie: String, path: String) {
        lock.withLock { responseCookies[path] = cookie }
    }

    static func requestSnapshot() -> [URLRequest] {
        lock.withLock { requests }
    }

    // URLProtocol requires these class overrides for custom protocol dispatch.
    // swiftlint:disable:next static_over_final_class
    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    // swiftlint:disable:next static_over_final_class
    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        var recordedRequest = request
        recordedRequest.httpBody = URLRequestHTTPBody.data(from: request)
        let configuration = Self.lock.withLock {
            Self.requests.append(recordedRequest)
            let status = switch path {
            case "/api/v1/auth/email-address/tokens": Self.otpStatusCode
            case "/api/v1/auth/logout": Self.logoutStatusCode
            case "/mutation": request.value(forHTTPHeaderField: "x-voucha-bot-challenge-token") == nil ? 403 : 200
            default: 200
            }
            return ResponseConfiguration(status: status, identity: Self.identity, cookie: Self.responseCookies[path])
        }
        let status: Int
        let data: Data
        status = path == "/api/v1/session" && !isCredentialedSessionRefresh(recordedRequest) ? 401 : configuration
            .status
        do {
            data = try responseData(path: path, status: status, identity: configuration.identity)
        } catch {
            client?.urlProtocol(self, didFailWithError: error)
            return
        }
        var headers = ["Content-Type": "application/json"]
        if let cookie = configuration.cookie { headers["Set-Cookie"] = cookie }
        if path == "/api/v1/auth/mfa/totp/verification", status == 200 {
            headers["Set-Cookie"] = "st=fixture-authenticated-session; Path=/; Secure; HttpOnly"
        }
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: status,
                  httpVersion: "HTTP/1.1",
                  headerFields: headers
              )
        else { return }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    private func responseData(path: String, status: Int, identity: Data) throws -> Data {
        switch path {
        case "/api/v1/auth/email-address/login":
            Data(#"{"mfaRequired":true,"loginAttemptId":"mfa-attempt"}"#.utf8)
        case "/api/v1/my/identity":
            identity
        case "/mutation":
            Data(#"{"code":"BOT_CHALLENGE_REQUIRED","action":"bot-challenge.mutation","message":"Verify"}"#.utf8)
        case "/api/v1/session":
            status == 401 ? Data(#"{"code":"AUTH_REQUIRED"}"#.utf8) : try androidSessionRefreshFixture()
        default:
            Data("{}".utf8)
        }
    }

    override func stopLoading() {}
}

final class MemorySecrets: AndroidEndpointSecretStoring {
    var values: [String: String] = [:]

    func string(forKey key: String) throws -> String? {
        values[key]
    }

    func set(_ value: String, forKey key: String) throws {
        values[key] = value
    }

    func removeValue(forKey key: String) throws {
        values.removeValue(forKey: key)
    }
}

func androidSessionRefreshFixture() throws -> Data {
    let path = "api-fixtures/v1/responses/native.auth.session.refresh.rotated.json"
    let root = try ContractRoot.url(requiredPaths: [path])
    return try Data(contentsOf: ContractRoot.requiredURL(path, root: root))
}

func isCredentialedSessionRefresh(_ request: URLRequest) -> Bool {
    guard request.httpMethod == "PATCH",
          let body = URLRequestHTTPBody.data(from: request),
          let tokens = try? JSONDecoder().decode([String: String].self, from: body),
          !tokens.isEmpty, tokens.keys.allSatisfy({ ["dt", "st"].contains($0) }),
          tokens.values.allSatisfy({ !$0.isEmpty }) else { return false }
    let cookies = Dictionary(
        (request.value(forHTTPHeaderField: "Cookie") ?? "")
            .split(separator: ";").compactMap { component -> (String, String)? in
                let parts = component.trimmingCharacters(in: .whitespaces).split(separator: "=", maxSplits: 1)
                guard parts.count == 2 else { return nil }
                return (String(parts[0]), String(parts[1]))
            },
        uniquingKeysWith: { _, last in last }
    )
    return tokens == cookies.filter { ["dt", "st"].contains($0.key) }
}

func androidRotatedCookieValues() throws -> [String] {
    let response = try JSONSerialization.jsonObject(with: androidSessionRefreshFixture()) as? [String: Any]
    let tokens = try XCTUnwrap(response?["session"] as? [String: Any])
    return try [XCTUnwrap(tokens["dt"] as? String), XCTUnwrap(tokens["st"] as? String)].sorted()
}

func seedAndroidSessionCredentials(_ storage: AndroidSessionCookieStorage, origin: URL) throws {
    let host = try XCTUnwrap(origin.host)
    for (name, value) in [("dt", "existing-device-session"), ("st", "existing-member-session")] {
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .domain: host, .path: "/", .name: name, .value: value, .secure: "TRUE"
        ])))
    }
}
