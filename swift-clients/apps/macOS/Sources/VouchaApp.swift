import SwiftUI
import VouchaAuth
import VouchaDesignSystem

@main
struct VouchaApp: App {
    @State
    private var factory = ViewModelFactory.makeDefault()

    var body: some Scene {
        if Self.presentsUITestingWindow {
            if #available(macOS 15, *) {
                mainWindow.defaultLaunchBehavior(.presented)
            } else {
                mainWindow
            }
        } else {
            mainWindow.commands {
                CommandGroup(replacing: .newItem) {}
            }
        }
    }

    private var mainWindow: some Scene {
        WindowGroup {
            AppLaunchContent(viewModelFactory: factory)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)
    }

    /// UI-test launches must present a window. Replacing the New command leaves a later
    /// launch in the same session with no way to open one.
    private static var presentsUITestingWindow: Bool {
        #if DEBUG
            ProcessInfo.processInfo.arguments.contains { $0.hasPrefix("--ui-testing-") }
        #else
            false
        #endif
    }
}
