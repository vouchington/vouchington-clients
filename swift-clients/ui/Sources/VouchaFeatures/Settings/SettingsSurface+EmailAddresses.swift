import SwiftUI

extension SettingsSurface {
    @ViewBuilder
    var emailAddressesSection: some View {
        if let client = viewModel.client {
            section(.nativeSwiftSettingsEmailAddresses, systemImage: "envelope") {
                EmailAddressManager(client: client)
            }
        }
    }
}
