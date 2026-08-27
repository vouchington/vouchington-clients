import SwiftUI
import VouchaAuth
import VouchaDesignSystem

@main
struct VouchaApp: App {
    @State
    private var factory = ViewModelFactory.makeDefault()

    var body: some Scene {
        WindowGroup {
            AppLaunchContent(viewModelFactory: factory)
        }
        .windowStyle(.titleBar)
        .windowToolbarStyle(.unified)
        .commands {
            CommandGroup(replacing: .newItem) {}
        }
    }
}
