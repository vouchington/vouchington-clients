#if DEBUG
    import Foundation
    import VouchaAPI
    import VouchaAuth
    import VouchaCore
    import VouchaPersistence

    @MainActor
    enum NotificationSettingsUITestingFactory {
        static func make() -> ViewModelFactory {
            guard let baseURL = URL(string: "https://ui-testing.voucha.invalid") else {
                preconditionFailure("The UI-testing fixture URL must be valid")
            }
            let config = AppConfig(baseURL: baseURL)
            let configuration = URLSessionConfiguration.ephemeral
            guard let cookies = configuration.httpCookieStorage else {
                preconditionFailure("UI testing requires an ephemeral cookie store")
            }
            let client = APIClient(
                config: config,
                cookieStorage: cookies,
                protocolClasses: [NotificationSettingsUITestingURLProtocol.self],
                bootstrapSession: false,
                sessionConfiguration: configuration
            )
            return ViewModelFactory(
                apiClient: client,
                sessionManager: SessionManager(client: client),
                cacheStore: MemoryDiskCacheStore(),
                nativeOAuthAuthorizationStore: NativeOAuthAuthorizationStore(
                    secureState: UITestingOAuthAuthorizationSecureState()
                ),
                restoresSessionOnLaunch: false
            )
        }
    }

    private final class NotificationSettingsUITestingURLProtocol: URLProtocol {
        override static func canInit(with _: URLRequest) -> Bool {
            true
        }

        override static func canonicalRequest(for request: URLRequest) -> URLRequest {
            request
        }

        private let deliveryLock = NSRecursiveLock()
        private var stopped = false

        override func startLoading() {
            deliveryLock.lock()
            defer { deliveryLock.unlock() }
            guard !stopped else { return }
            guard let url = request.url, url.host == "ui-testing.voucha.invalid",
                  let data = responseData(for: url),
                  let response = HTTPURLResponse(
                      url: url, statusCode: 200, httpVersion: "HTTP/1.1",
                      headerFields: ["Content-Type": "application/json"]
                  ) else {
                client?.urlProtocol(self, didFailWithError: URLError(.unsupportedURL))
                return
            }
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            guard !stopped else { return }
            client?.urlProtocol(self, didLoad: data)
            guard !stopped else { return }
            client?.urlProtocolDidFinishLoading(self)
        }

        private func responseData(for url: URL) -> Data? {
            if url.path == "/api/v1/session" {
                guard request.httpMethod == "PATCH",
                      let encoded = ProcessInfo.processInfo.environment["VOUCHA_UI_TESTING_SESSION_RESPONSE_BASE64"]
                else { return nil }
                return Data(base64Encoded: encoded)
            }
            return switch url.path {
            case "/api/v1/my/identity": Self.identity
            case "/api/v1/my/email-preferences": Self.emailPreferences
            case "/api/v1/feature-flags": Data(#"{"flags":{},"overrides":null}"#.utf8)
            case "/api/v1/my/profile": Data(#"{"profile":{"id":"user-1","markdown":""}}"#.utf8)
            default: Data(#"{}"#.utf8)
            }
        }

        override func stopLoading() {
            deliveryLock.lock()
            stopped = true
            deliveryLock.unlock()
        }

        private static let identity = Data(
            """
            {"identity":{
              "__entity_type":"user", "id":"user-1", "username":"ui-test", "roles":[],
              "profile_image_id":null, "markdown":null, "email_address":"ui-test@example.com",
              "membership_plan":null, "verification_status":"verified", "suspended_at":null,
              "cards_visibility":"everyone", "rewards_program_statuses_visibility":"everyone",
              "spending_categories_visibility":"everyone", "follows_visibility":"everyone",
              "topic_follows_visibility":"everyone", "rss_feed_follows_visibility":"everyone",
              "community_memberships_visibility":"everyone", "followers_visibility":"everyone",
              "likes_visibility":"everyone", "direct_messages_audience":"users",
              "default_post_broadcast":"everyone", "default_post_privacy":"public",
              "is_engagement_emails_enabled":true, "news_digest_frequency":"weekly",
              "is_moderation_emails_enabled":true, "community_digest_frequency":"weekly",
              "moderation_email_cadence":"daily", "moderation_email_days_of_week":[1],
              "moderation_email_time_of_day":"09:00", "moderation_email_timezone":"UTC",
              "processing_restricted_at":null, "third_party_marketing":null, "country":null,
              "ui_locale":"en"
            }}
            """.utf8
        )
        private static let emailPreferences = Data(
            """
            {"email_preferences":{"is_engagement_emails_enabled":true,"news_digest_frequency":"weekly",
            "is_moderation_emails_enabled":true,"community_digest_frequency":"weekly",
            "moderation_email_cadence":"daily","moderation_email_days_of_week":[1],
            "moderation_email_time_of_day":"09:00","moderation_email_timezone":"UTC"}}
            """.utf8
        )
    }

#endif
