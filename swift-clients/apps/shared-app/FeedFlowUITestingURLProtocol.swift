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
                  let data = Self.canonicalResponseData(for: request, environment: scenario.environment),
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
            if Self
                .responseEnvironmentKey(for: request, environment: scenario.environment) ==
                "VOUCHA_UI_TESTING_FEED_RESPONSE_BASE64",
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

        static func canonicalResponseData(for request: URLRequest, environment: [String: String]) -> Data? {
            if request.url?.path == "/api/v1/session" {
                return sessionResponseData(for: request, environment: environment)
            }
            if isSourceCreationFlow(environment: environment) {
                return SourceCreationUITestingResponses.data(for: request)
            }
            return canonicalFeedResponseData(for: request, environment: environment)
        }

        private static func canonicalFeedResponseData(for request: URLRequest, environment: [String: String]) -> Data? {
            guard let key = responseEnvironmentKey(for: request, environment: environment),
                  let encoded = environment[key] else { return nil }
            return Data(base64Encoded: encoded)
        }

        private static func responseEnvironmentKey(for request: URLRequest, environment: [String: String]) -> String? {
            guard request.httpMethod == "GET",
                  let url = request.url,
                  url.host == "ui-testing.voucha.invalid",
                  let components = URLComponents(url: url, resolvingAgainstBaseURL: false)
            else { return nil }
            #if os(iOS)
                guard admitsNewsFlowRequest(components, environment: environment) else { return nil }
            #endif
            switch url.path {
            case "/api/v1/my/identity":
                guard components.queryItems?.isEmpty != false else { return nil }
                return "VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"
            case "/api/v1/feeds/rss_feed_items/any":
                return feedResponseEnvironmentKey(components, environment: environment)
            case "/api/v1/feeds/posts/any":
                return isPostsQuery(components) ? "VOUCHA_UI_TESTING_POSTS_FEED_RESPONSE_BASE64" : nil
            default:
                return nil
            }
        }

        private static func isPostsQuery(_ components: URLComponents) -> Bool {
            components.queryItems?.count == 3
                && components.queryItems?.contains(URLQueryItem(name: "limit", value: "20")) == true
                && components.queryItems?.contains(URLQueryItem(name: "sort", value: "hot")) == true
                && components.queryItems?.contains(URLQueryItem(name: "time_range", value: "1w")) == true
        }

        private static func sessionResponseData(for request: URLRequest, environment: [String: String]) -> Data? {
            guard request.httpMethod == "PATCH", request.url?.host == "ui-testing.voucha.invalid",
                  let encoded = environment["VOUCHA_UI_TESTING_SESSION_RESPONSE_BASE64"]
            else { return nil }
            return Data(base64Encoded: encoded)
        }

        private static func isSourceCreationFlow(environment: [String: String]) -> Bool {
            environment["VOUCHA_UI_TESTING_SOURCE_CREATION_RESPONSES"] != nil
                && environment["VOUCHA_UI_TESTING_NEWS_FEED_RESPONSE_BASE64"] == nil
        }

        #if os(iOS)
            private static func admitsNewsFlowRequest(
                _ components: URLComponents, environment: [String: String]
            ) -> Bool {
                guard environment["VOUCHA_UI_TESTING_NEWS_FEED_RESPONSE_BASE64"] != nil else { return true }
                if components.path == "/api/v1/my/identity" { return true }
                return components.path == "/api/v1/feeds/rss_feed_items/any"
                    && components.queryItems?.contains(URLQueryItem(name: "media_type", value: "article")) == true
            }
        #endif

        private static func feedResponseEnvironmentKey(
            _ components: URLComponents, environment: [String: String]
        ) -> String? {
            guard components.queryItems?.count == 2,
                  components.queryItems?.contains(URLQueryItem(name: "limit", value: "20")) == true,
                  let mediaType = components.queryItems?.first(where: { $0.name == "media_type" })?.value
            else { return nil }
            switch mediaType {
            #if os(iOS)
                case "article":
                    guard environment["VOUCHA_UI_TESTING_NEWS_FEED_RESPONSE_BASE64"] != nil else { return nil }
                    return "VOUCHA_UI_TESTING_NEWS_FEED_RESPONSE_BASE64"
            #endif
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
