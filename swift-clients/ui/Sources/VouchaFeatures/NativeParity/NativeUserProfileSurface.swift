import SwiftUI
import VouchaDesignSystem
import VouchaModels

struct NativeUserProfileSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let entry: NativeRouteCatalogEntry
    @Bindable
    var viewModel: NativeRouteSurfaceViewModel
    let isSignedIn: Bool
    let isAdministrator: Bool
    let currentUserId: String?
    let isStaffViewer: Bool
    let canCastPublicVotes: Bool
    let hideDownCount: Bool
    let turnstileSiteKey: String?
    let showSignIn: () -> Void
    let onNavigate: (String) -> Void

    init(
        entry: NativeRouteCatalogEntry,
        viewModel: NativeRouteSurfaceViewModel,
        isSignedIn: Bool,
        isAdministrator: Bool = false,
        currentUserId: String? = nil,
        isStaffViewer: Bool = false,
        canCastPublicVotes: Bool = false,
        hideDownCount: Bool = false,
        turnstileSiteKey: String?,
        showSignIn: @escaping () -> Void,
        onNavigate: @escaping (String) -> Void
    ) {
        self.entry = entry
        self.viewModel = viewModel
        self.isSignedIn = isSignedIn
        self.isAdministrator = isAdministrator
        self.currentUserId = currentUserId
        self.isStaffViewer = isStaffViewer
        self.canCastPublicVotes = canCastPublicVotes
        self.hideDownCount = hideDownCount
        self.turnstileSiteKey = turnstileSiteKey
        self.showSignIn = showSignIn
        self.onNavigate = onNavigate
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            if let profile = viewModel.userProfile.header {
                profileHeader(profile)
                primaryTabs(profile)
                contextualTabs
            }
            NativeDetailSurface(
                entry: entry,
                viewModel: viewModel,
                isSignedIn: isSignedIn,
                canCastPublicVotes: canCastPublicVotes,
                turnstileSiteKey: turnstileSiteKey,
                showSignIn: showSignIn,
                onNavigate: onNavigate,
                showsRows: currentScope == .overview
            )
            if currentScope != .overview {
                scopedCollection
            }
            paginationControls
        }
    }

}
