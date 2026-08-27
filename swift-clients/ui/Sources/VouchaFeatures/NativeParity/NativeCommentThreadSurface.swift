import SwiftUI
import VouchaAPI

struct NativeCommentThreadSurface: View {
    @Environment(\.locale)
    var nativeUiLocale
    let client: APIClient?
    let routeMatch: NativeRouteMatch?
    let currentUserId: String?
    let isSignedIn: Bool
    let canCreateVote: Bool
    let hideDownCount: Bool
    let turnstileSiteKey: String?
    let showSignIn: () -> Void
    @State
    var viewModel: NativeCommentThreadViewModel
    @State
    var composer: NativeCommentThreadComposerState?
    @State var showingTurnstile = false

    init(
        client: APIClient?,
        routeMatch: NativeRouteMatch?,
        currentUserId: String? = nil,
        isSignedIn: Bool,
        canCreateVote: Bool = true,
        hideDownCount: Bool = false,
        turnstileSiteKey: String? = nil,
        composer: NativeCommentThreadComposerState? = nil,
        showSignIn: @escaping () -> Void
    ) {
        self.client = client
        self.routeMatch = routeMatch
        self.currentUserId = currentUserId
        self.isSignedIn = isSignedIn
        self.canCreateVote = canCreateVote
        self.hideDownCount = hideDownCount
        self.turnstileSiteKey = turnstileSiteKey
        self.showSignIn = showSignIn
        _composer = State(initialValue: composer)
        _viewModel = State(
            initialValue: NativeCommentThreadViewModel(
                client: client,
                rootPostId: routeMatch?.param("id") ?? routeMatch?.path.routeLastSegment ?? "",
                currentUserId: currentUserId
            )
        )
    }
}
