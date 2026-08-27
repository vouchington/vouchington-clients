import SwiftUI
import VouchaAPI

struct LandingPagesNativeSurface: View {
    @State
    private var viewModel: LandingPagesViewModel

    init(client: APIClient?, initialSlug: String?, canViewAnalytics: Bool) {
        _viewModel = State(wrappedValue: LandingPagesViewModel(
            client: client,
            initialSlug: initialSlug,
            canViewAnalytics: canViewAnalytics,
            requiresAuthoritativeAnalyticsMembership: true
        ))
    }

    var body: some View {
        LandingPagesView(viewModel: viewModel)
    }
}
