import Foundation

enum SignInUITestingLaunch {
    static let launchArgument = "--ui-testing-sign-in"

    static var turnstileToken: String? {
        #if DEBUG
            guard ProcessInfo.processInfo.arguments.contains(launchArgument) else { return nil }
            return nonEmptyEnvironmentValue("VOUCHA_UI_TESTING_TURNSTILE_TOKEN")
        #else
            return nil
        #endif
    }

    static func nonEmptyEnvironmentValue(_ key: String) -> String? {
        let value = ProcessInfo.processInfo.environment[key]
        guard let value, !value.isEmpty else { return nil }
        return value
    }
}

#if DEBUG
    /// Serves the email OTP sign-in calls for one isolated iPhone control flow.
    final class SignInUITestingURLProtocol: URLProtocol {
        static let launchArgument = SignInUITestingLaunch.launchArgument
        private let deliveryLock = NSRecursiveLock()
        private var stopped = false

        override static func canInit(with _: URLRequest) -> Bool {
            true
        }

        override static func canonicalRequest(for request: URLRequest) -> URLRequest {
            request
        }

        override func startLoading() {
            deliveryLock.lock()
            defer { deliveryLock.unlock() }
            guard !stopped else { return }
            guard let url = request.url,
                  let data = responseData(for: url),
                  let response = HTTPURLResponse(
                      url: url,
                      statusCode: 200,
                      httpVersion: "HTTP/1.1",
                      headerFields: ["Content-Type": "application/json"]
                  )
            else {
                client?.urlProtocol(self, didFailWithError: URLError(.unsupportedURL))
                return
            }
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            guard !stopped else { return }
            client?.urlProtocol(self, didLoad: data)
            guard !stopped else { return }
            client?.urlProtocolDidFinishLoading(self)
        }

        override func stopLoading() {
            deliveryLock.lock()
            stopped = true
            deliveryLock.unlock()
        }

        private func responseData(for url: URL) -> Data? {
            guard url.host == "ui-testing.voucha.invalid" else { return nil }
            switch (request.httpMethod ?? "", url.path) {
            case ("GET", "/api/v1/my/identity"):
                return Self.base64EnvironmentValue("VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64")
            case ("GET", "/api/v1/auth/oauth/providers"):
                return Self.base64EnvironmentValue("VOUCHA_UI_TESTING_OAUTH_PROVIDERS_RESPONSE_BASE64")
            case ("POST", "/api/v1/auth/email-address/tokens"):
                return acceptedTokenRequest()
            case ("POST", "/api/v1/auth/email-address/login"):
                return acceptedLogin()
            default:
                return nil
            }
        }

        private func acceptedTokenRequest() -> Data? {
            let body = jsonBody()
            guard body["email_address"] == SignInUITestingLaunch.nonEmptyEnvironmentValue(
                "VOUCHA_UI_TESTING_SIGN_IN_EMAIL"
            ),
                body["cf_turnstile_response"] == SignInUITestingLaunch.nonEmptyEnvironmentValue(
                    "VOUCHA_UI_TESTING_TURNSTILE_TOKEN"
                )
            else { return nil }
            return Data("{}".utf8)
        }

        private func acceptedLogin() -> Data? {
            let body = jsonBody()
            guard body["email_address"] == SignInUITestingLaunch.nonEmptyEnvironmentValue(
                "VOUCHA_UI_TESTING_SIGN_IN_EMAIL"
            ),
                body["token"] == SignInUITestingLaunch.nonEmptyEnvironmentValue("VOUCHA_UI_TESTING_SIGN_IN_CODE")
            else { return nil }
            return Data(#"{"mfa_required":false}"#.utf8)
        }

        private func jsonBody() -> [String: String] {
            guard let data = bodyData(),
                  let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any]
            else { return [:] }
            return object.compactMapValues { $0 as? String }
        }

        private func bodyData() -> Data? {
            if let body = request.httpBody, !body.isEmpty { return body }
            guard let stream = request.httpBodyStream else { return nil }
            stream.open()
            defer { stream.close() }
            var data = Data()
            let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: 1_024)
            defer { buffer.deallocate() }
            while stream.hasBytesAvailable {
                let count = stream.read(buffer, maxLength: 1_024)
                if count <= 0 { break }
                data.append(buffer, count: count)
            }
            return data.isEmpty ? nil : data
        }

        private static func base64EnvironmentValue(_ key: String) -> Data? {
            guard let encoded = SignInUITestingLaunch.nonEmptyEnvironmentValue(key) else { return nil }
            return Data(base64Encoded: encoded)
        }

    }
#endif
