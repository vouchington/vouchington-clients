import Foundation
import SwiftUI
import VouchaCore
import VouchaFeatures

struct AppLaunchContent: View {
    static let fediverseUITestingArgument = "--ui-testing-fediverse"
    static let notificationSettingsUITestingArgument = "--ui-testing-notification-settings"

    let viewModelFactory: ViewModelFactory

    var body: some View {
        launchContent
            .task(id: viewModelFactory.sessionManager.currentUserId) {
                SentryAppSupport.applyAccountId(viewModelFactory.sessionManager.currentUserId)
            }
    }

    @ViewBuilder
    private var launchContent: some View {
        #if DEBUG
            if ProcessInfo.processInfo.arguments.contains(FeedFlowUITestingURLProtocol.launchArgument) {
                FeedFlowUITestingFixture(viewModelFactory: viewModelFactory)
            } else if Self.isNotificationSettingsFixtureEnabled {
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

#endif
