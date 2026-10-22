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
            let outcome = loadOutcome()
            // Login completion refreshes identity on this session. Leave startLoading
            // before delivery so that follow-up is not stuck on URLSession's protocol queue.
            // The callback itself stays under deliveryLock so cancellation cannot pass it.
            DispatchQueue.global(qos: .userInitiated).async {
                self.deliver(outcome)
            }
        }

        private enum LoadOutcome {
            case empty
            case failure(URLError)
            case success(HTTPURLResponse, Data)
        }

        private func loadOutcome() -> LoadOutcome {
            deliveryLock.lock()
            let alreadyStopped = stopped
            deliveryLock.unlock()
            guard !alreadyStopped else { return .empty }
            guard let url = request.url,
                  let data = responseData(for: url),
                  let response = HTTPURLResponse(
                      url: url,
                      statusCode: 200,
                      httpVersion: "HTTP/1.1",
                      headerFields: ["Content-Type": "application/json"]
                  )
            else { return .failure(URLError(.unsupportedURL)) }
            return .success(response, data)
        }

        private func deliver(_ outcome: LoadOutcome) {
            switch outcome {
            case .empty:
                return
            case let .failure(error):
                deliverIfNotStopped { client?.urlProtocol(self, didFailWithError: error) }
            case let .success(response, data):
                deliverIfNotStopped {
                    client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
                }
                deliverIfNotStopped { client?.urlProtocol(self, didLoad: data) }
                deliverIfNotStopped { client?.urlProtocolDidFinishLoading(self) }
            }
        }

        private func deliverIfNotStopped(_ callback: () -> Void) {
            deliveryLock.lock()
            defer { deliveryLock.unlock() }
            guard !stopped else { return }
            callback()
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
            // verifyEmailOTP encodes a dictionary. Some JSONEncoder versions leave
            // dictionary keys as emailAddress; others rewrite them to email_address.
            let email = body["emailAddress"] ?? body["email_address"]
            guard email == SignInUITestingLaunch.nonEmptyEnvironmentValue("VOUCHA_UI_TESTING_SIGN_IN_EMAIL"),
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
