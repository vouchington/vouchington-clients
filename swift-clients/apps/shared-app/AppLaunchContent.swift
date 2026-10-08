import Foundation
import SwiftUI
import VouchaAPI
import VouchaAuth
import VouchaCore
import VouchaFeatures
import VouchaPersistence

struct AppLaunchContent: View {
    static let fediverseUITestingArgument = "--ui-testing-fediverse"
    static let notificationSettingsUITestingArgument = "--ui-testing-notification-settings"

    let viewModelFactory: ViewModelFactory

    var body: some View {
        #if DEBUG
            if Self.isNotificationSettingsFixtureEnabled {
                NotificationSettingsUITestingFixture()
            } else if ProcessInfo.processInfo.arguments.contains(Self.fediverseUITestingArgument),
                      let route = NativeRouteCatalog.matchingRoute(for: "/fediverse") {
                NavigationStack {
                    NativeRouteDestinationView(
                        entry: route.entry,
                        routeMatch: route.match,
                        isSignedIn: false,
                        canCastPublicVotes: false
                    )
                }
            } else {
                RootView(viewModelFactory: viewModelFactory)
            }
        #else
            RootView(viewModelFactory: viewModelFactory)
        #endif
    }

    #if DEBUG
        private static var isUITestingFixtureEnabled: Bool {
            ProcessInfo.processInfo.arguments.contains(fediverseUITestingArgument)
                || isNotificationSettingsFixtureEnabled
        }

        private static var isNotificationSettingsFixtureEnabled: Bool {
            ProcessInfo.processInfo.arguments.contains(notificationSettingsUITestingArgument)
        }
    #endif
}

#if DEBUG
    private struct NotificationSettingsUITestingFixture: View {
        @State
        private var routeDispatch: RootView.ExternalRouteDispatch?
        @State
        private var announcementCount = 0
        @State
        private var announcement = ""
        @State
        private var reentryGeneration = 0
        private let viewModelFactory = NotificationSettingsUITestingFactory.make()

        var body: some View {
            let announcementCount = $announcementCount
            let announcement = $announcement
            RootView(
                viewModelFactory: viewModelFactory,
                externalRouteDispatch: routeDispatch
            )
            .task {
                _ = await viewModelFactory.sessionManager.refresh()
                dispatchNotificationSettingsRoute()
            }
            .safeAreaInset(edge: .bottom) {
                Button {
                    reentryGeneration += 1
                    dispatchNotificationSettingsRoute()
                } label: {
                    Image(systemName: "arrow.clockwise")
                }
                .accessibilityIdentifier("notification-settings-reenter-route")
                Text(verbatim: String(announcementCount.wrappedValue))
                    .accessibilityIdentifier("notification-settings-route-activation-count")
                Text(verbatim: announcement.wrappedValue)
                    .accessibilityIdentifier("notification-settings-route-announcement")
            }
            .onReceive(NotificationCenter.default
                .publisher(for: NotificationSettingsUITestingHooks.activated)) { note in
                    announcementCount.wrappedValue += 1
                    announcement.wrappedValue = note.object as? String ?? ""
            }
        }

        private func dispatchNotificationSettingsRoute() {
            routeDispatch = .init(
                targetPath: "/my/notification-settings",
                generation: reentryGeneration
            )
        }
    }

    @MainActor
    private enum NotificationSettingsUITestingFactory {
        static func make() -> ViewModelFactory {
            guard let baseURL = URL(string: "https://ui-testing.voucha.invalid") else {
                preconditionFailure("The UI-testing fixture URL must be valid")
            }
            let config = AppConfig(baseURL: baseURL)
            let cookies = HTTPCookieStorage()
            let client = APIClient(
                config: config,
                cookieStorage: cookies,
                protocolClasses: [NotificationSettingsUITestingURLProtocol.self],
                bootstrapSession: false
            )
            return ViewModelFactory(
                apiClient: client,
                sessionManager: SessionManager(client: client, cookieStorage: cookies),
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

        override func startLoading() {
            let data = switch request.url?.path {
            case "/api/v1/my/identity": Self.identity
            case "/api/v1/my/email-preferences": Self.emailPreferences
            case "/api/v1/feature-flags": Data(#"{"flags":{},"overrides":null}"#.utf8)
            case "/api/v1/my/profile": Data(#"{"profile":{"id":"user-1","markdown":""}}"#.utf8)
            default: Data(#"{}"#.utf8)
            }
            guard let url = request.url,
                  let response = HTTPURLResponse(
                      url: url,
                      statusCode: 200,
                      httpVersion: "HTTP/1.1",
                      headerFields: ["Content-Type": "application/json"]
                  )
            else { return }
            client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
            client?.urlProtocol(self, didLoad: data)
            client?.urlProtocolDidFinishLoading(self)
        }

        override func stopLoading() {}

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
