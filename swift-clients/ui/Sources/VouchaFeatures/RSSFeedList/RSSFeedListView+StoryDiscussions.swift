import SwiftUI
import VouchaLocalization

extension RSSFeedListView {
    func storyDiscussionDestinationView(_ destination: StoryDiscussionDestination) -> some View {
        NativeRouteDestinationView(
            entry: NativeParityCatalog.entry(for: .postDetail),
            client: viewModel.client,
            routeMatch: destination.routeMatch,
            isSignedIn: isSignedIn,
            currentUserId: currentUserId,
            showSignIn: showSignIn ?? {},
            canCastPublicVotes: canVote
        )
    }

    func openStoryDiscussionButton(_ destination: StoryDiscussionDestination) -> some View {
        Button {
            storyDiscussionDestination = destination
        } label: {
            Label(
                UiMessages.string(.nativeSwiftRssFeedListOpenDiscussion, locale: nativeUiLocale),
                systemImage: "arrow.right.circle"
            )
        }
        .buttonStyle(.bordered)
    }
}
