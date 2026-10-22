import AppKit
import SwiftUI
import VouchaAuth
import VouchaDesignSystem

@main
struct VouchaApp: App {
    @NSApplicationDelegateAdaptor(VouchaUITestingWindowDelegate.self) private var uiTestingDelegate
    @State
    private var factory = ViewModelFactory.makeDefault()

    var body: some Scene {
        WindowGroup {
            AppLaunchContent(viewModelFactory: factory)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)
        .commands {
            if !Self.presentsUITestingWindow {
                CommandGroup(replacing: .newItem) {}
            }
        }
    }

    /// UI-test launches need the system New command. A later launch in the same
    /// session does not open a window when that command has been removed.
    private static var presentsUITestingWindow: Bool {
        #if DEBUG
            ProcessInfo.processInfo.arguments.contains { $0.hasPrefix("--ui-testing-") }
        #else
            false
        #endif
    }
}

/// Opens the main window when a UI-test process finishes launch without one.
final class VouchaUITestingWindowDelegate: NSObject, NSApplicationDelegate {
    private var attempts = 0

    func applicationDidFinishLaunching(_: Notification) {
        guard Self.isUITestingLaunch else { return }
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.5) { [weak self] in
            self?.openWindowIfNeeded()
        }
    }

    private func openWindowIfNeeded() {
        if let window = NSApp.windows.first(where: \.canBecomeKey) {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }
        if let item = Self.newWindowItem(), let action = item.action {
            NSApp.sendAction(action, to: item.target, from: item)
            return
        }
        guard attempts < 20 else { return }
        attempts += 1
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.1) { [weak self] in
            self?.openWindowIfNeeded()
        }
    }

    private static func newWindowItem() -> NSMenuItem? {
        NSApp.mainMenu?.items.first { $0.title == "File" }?
            .submenu?.items.first { $0.title == "New Window" || $0.title == "New" }
    }

    private static var isUITestingLaunch: Bool {
        #if DEBUG
            ProcessInfo.processInfo.arguments.contains { $0.hasPrefix("--ui-testing-") }
        #else
            false
        #endif
    }
}
