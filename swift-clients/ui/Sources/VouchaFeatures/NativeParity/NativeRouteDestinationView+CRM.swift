import SwiftUI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    @ViewBuilder
    var crmDestinationContent: some View {
        if let client = viewModel.client {
            CRMContactsSurface(client: client, routeMatch: viewModel.routeMatch)
        } else {
            EmptyStateView(
                icon: "person.text.rectangle",
                title: .message(.nativeSwiftEmptyStateCrm),
                message: .message(.nativeSwiftEmptyStateCrmStaffMessage)
            )
        }
    }
}
