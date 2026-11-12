#if DEBUG
    import Foundation

    /// Immutable canonical response input; request/cancellation state belongs to this protocol instance.
    final class FeedFlowUITestingURLProtocol: URLProtocol {
        static let launchArgument = "--ui-testing-feed-flow"
        /// One immutable response map and one synchronized fault budget per app launch.
        final class Scenario {
            static let process = Scenario(environment: ProcessInfo.processInfo.environment)
            let environment: [String: String]
            private let lock = NSLock()
            private var videoFailurePending: Bool

            init(environment: [String: String]) {
                self.environment = environment
                videoFailurePending = environment["VOUCHA_UI_TESTING_VIDEO_FAIL_ONCE"] == "1"
            }

            func consumeVideoFailure() -> Bool {
                lock.lock()
                defer { lock.unlock() }
                guard videoFailurePending else { return false }
                videoFailurePending = false
                return true
            }
        }

        var scenario = Scenario.process
        private let deliveryLock = NSRecursiveLock()
        private var stopped = false

        override static func canInit(with _: URLRequest) -> Bool {
            // This isolated session must never fall through to live API or image hosts.
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
                  let data = canonicalResponseData(),
                  let response = HTTPURLResponse(
                      url: url,
                      statusCode: request.httpMethod == "POST" ? 202 : 200,
                      httpVersion: "HTTP/1.1",
                      headerFields: ["Content-Type": "application/json"]
                  )
            else {
                client?.urlProtocol(self, didFailWithError: URLError(.unsupportedURL))
                return
            }
            if responseEnvironmentKey() == "VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64",
               scenario.consumeVideoFailure() {
                client?.urlProtocol(self, didFailWithError: URLError(.notConnectedToInternet))
                return
            }
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            guard !stopped else { return }
            client?.urlProtocol(self, didLoad: data)
            guard !stopped else { return }
            client?.urlProtocolDidFinishLoading(self)
        }

        private func canonicalResponseData() -> Data? {
            if request.url?.path == "/api/v1/session" { return sessionResponseData() }
            if ProcessInfo.processInfo.environment["VOUCHA_UI_TESTING_SOURCE_CREATION_RESPONSES"] != nil {
                return SourceCreationUITestingResponses.data(for: request)
            }
            guard let key = responseEnvironmentKey(), let encoded = scenario.environment[key] else { return nil }
            return Data(base64Encoded: encoded)
        }

        private func responseEnvironmentKey() -> String? {
            guard request.httpMethod == "GET",
                  let url = request.url,
                  url.host == "ui-testing.voucha.invalid",
                  let components = URLComponents(url: url, resolvingAgainstBaseURL: false)
            else { return nil }
            return responseEnvironmentKey(url: url, components: components)
        }

        private func responseEnvironmentKey(url: URL, components: URLComponents) -> String? {
            switch url.path {
            case "/api/v1/my/identity":
                guard components.queryItems?.isEmpty != false else { return nil }
                return "VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"
            case "/api/v1/feeds/rss_feed_items/any":
                return feedResponseEnvironmentKey(components)
            case "/api/v1/feeds/posts/any":
                return isPostsQuery(components) ? "VOUCHA_UI_TESTING_POSTS_FEED_RESPONSE_BASE64" : nil
            default:
                return nil
            }
        }

        private func isPostsQuery(_ components: URLComponents) -> Bool {
            components.queryItems?.count == 3
                && components.queryItems?.contains(URLQueryItem(name: "limit", value: "20")) == true
                && components.queryItems?.contains(URLQueryItem(name: "sort", value: "hot")) == true
                && components.queryItems?.contains(URLQueryItem(name: "time_range", value: "1w")) == true
        }

        private func sessionResponseData() -> Data? {
            guard request.httpMethod == "PATCH", request.url?.host == "ui-testing.voucha.invalid",
                  let encoded = scenario.environment["VOUCHA_UI_TESTING_SESSION_RESPONSE_BASE64"]
            else { return nil }
            return Data(base64Encoded: encoded)
        }

        private func feedResponseEnvironmentKey(_ components: URLComponents) -> String? {
            guard components.queryItems?.count == 2,
                  components.queryItems?.contains(URLQueryItem(name: "limit", value: "20")) == true,
                  let mediaType = components.queryItems?.first(where: { $0.name == "media_type" })?.value
            else { return nil }
            switch mediaType {
            case "video":
                return "VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64"
            case "audio":
                return "VOUCHA_UI_TESTING_PODCAST_FEED_RESPONSE_BASE64"
            default:
                return nil
            }
        }

        override func stopLoading() {
            deliveryLock.lock()
            stopped = true
            deliveryLock.unlock()
        }
    }
#endif
