#if DEBUG
    import Foundation

    /// Immutable canonical response input; request/cancellation state belongs to this protocol instance.
    final class FeedFlowUITestingURLProtocol: URLProtocol {
        static let launchArgument = "--ui-testing-feed-flow"
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

        private func canonicalResponseData() -> Data? {
            guard request.httpMethod == "GET",
                  let url = request.url,
                  url.host == "ui-testing.voucha.invalid",
                  let components = URLComponents(url: url, resolvingAgainstBaseURL: false)
            else { return nil }
            let key: String
            switch url.path {
            case "/api/v1/my/identity":
                guard components.queryItems?.isEmpty != false else { return nil }
                key = "VOUCHA_UI_TESTING_IDENTITY_RESPONSE_BASE64"
            case "/api/v1/feeds/rss_feed_items/any":
                guard let responseKey = feedResponseEnvironmentKey(components) else { return nil }
                key = responseKey
            case "/api/v1/feeds/posts/any":
                guard components.queryItems?.count == 2,
                      components.queryItems?.contains(URLQueryItem(name: "limit", value: "20")) == true,
                      components.queryItems?.contains(URLQueryItem(name: "sort", value: "hot")) == true
                else { return nil }
                key = "VOUCHA_UI_TESTING_POSTS_FEED_RESPONSE_BASE64"
            default:
                return nil
            }
            guard let encoded = ProcessInfo.processInfo.environment[key] else { return nil }
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
