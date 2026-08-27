import SwiftUI
import VouchaAndroid
import VouchaCore

private typealias AppRootView = VouchaAndroidRootView
private typealias AppDelegate = VouchaAndroidAppDelegate
private let appLogger = VouchaLogger(category: "VouchaAndroidApp")

/// The entry point to the app simply loads the App implementation from SPM module.
@main
struct AppMain: App {
    @AppDelegateAdaptor(AppMainDelegate.self)
    var appDelegate
    @Environment(\.scenePhase)
    private var scenePhase

    var body: some Scene {
        WindowGroup {
            AppRootView()
        }
        .onChange(of: scenePhase) { _, newPhase in
            switch newPhase {
            case .active:
                AppDelegate.shared.onResume()
            case .inactive:
                AppDelegate.shared.onPause()
            case .background:
                AppDelegate.shared.onStop()
            @unknown default:
                appLogger.error("Unknown app phase: \(newPhase)")
            }
        }
    }
}

#if canImport(UIKit)
    typealias AppDelegateAdaptor = UIApplicationDelegateAdaptor
    typealias AppMainDelegateBase = UIApplicationDelegate
    typealias AppType = UIApplication
#elseif canImport(AppKit)
    typealias AppDelegateAdaptor = NSApplicationDelegateAdaptor
    typealias AppMainDelegateBase = NSApplicationDelegate
    typealias AppType = NSApplication
#endif

@MainActor
final class AppMainDelegate: NSObject, AppMainDelegateBase {
    let application = AppType.shared

    #if canImport(UIKit)
        func application(
            _: UIApplication,
            willFinishLaunchingWithOptions _: [UIApplication.LaunchOptionsKey: Any]? = nil
        ) -> Bool {
            AppDelegate.shared.onInit()
            return true
        }

        func application(
            _: UIApplication,
            didFinishLaunchingWithOptions _: [UIApplication.LaunchOptionsKey: Any]? = nil
        ) -> Bool {
            AppDelegate.shared.onLaunch()
            return true
        }

        func applicationWillTerminate(_: UIApplication) {
            AppDelegate.shared.onDestroy()
        }

        func applicationDidReceiveMemoryWarning(_: UIApplication) {
            AppDelegate.shared.onLowMemory()
        }

        // support for SkipNotify.fetchNotificationToken()

        func application(
            _ application: UIApplication,
            didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data
        ) {
            NotificationCenter.default.post(
                name: NSNotification.Name("didRegisterForRemoteNotificationsWithDeviceToken"),
                object: application,
                userInfo: ["deviceToken": deviceToken]
            )
        }

        func application(
            _ application: UIApplication,
            didFailToRegisterForRemoteNotificationsWithError error: any Error
        ) {
            NotificationCenter.default.post(
                name: NSNotification.Name("didFailToRegisterForRemoteNotificationsWithError"),
                object: application,
                userInfo: ["error": error]
            )
        }

    #elseif canImport(AppKit)

        func applicationWillFinishLaunching(_: Notification) {
            AppDelegate.shared.onInit()
        }

        func applicationDidFinishLaunching(_: Notification) {
            AppDelegate.shared.onLaunch()
        }

        func applicationWillTerminate(_: Notification) {
            AppDelegate.shared.onDestroy()
        }
    #endif

}
