import SwiftUI
import VouchaAPI

struct NativeDynamicConfigRouteSurface: View {
    let client: APIClient?
    let featureFlags: FeatureFlagState

    var body: some View {
        NativeDynamicConfigSurface(
            client: client,
            onNamespaceUpdated: featureFlags.applyGlobalNamespace
        )
    }
}
