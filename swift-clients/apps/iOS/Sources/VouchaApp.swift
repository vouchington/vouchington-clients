import SwiftUI
import VouchaAuth
import VouchaDesignSystem

@main
struct VouchaApp: App {
    @State
    private var factory: ViewModelFactory

    init() {
        SentryAppSupport.startIfConfigured()
        _factory = State(initialValue: ViewModelFactory.makeDefault())
    }

    var body: some Scene {
        WindowGroup {
            AppLaunchContent(viewModelFactory: factory)
        }
    }
}
