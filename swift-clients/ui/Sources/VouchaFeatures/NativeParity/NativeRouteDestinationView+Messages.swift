import SwiftUI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    var isMembershipGrantRoute: Bool {
        entry.destinationIdentifier == .membershipGrants
    }

    var isUserAdminRoute: Bool {
        entry.destinationIdentifier == .userAdmin
    }

    var isModmailMessagesRoute: Bool {
        viewModel.routeMatch?.path.hasPrefix("/messages/modmail/") == true
    }

    var isSignedInMessagesRoute: Bool {
        entry.destinationIdentifier == .messages
            && !isModmailMessagesRoute
            && isSignedIn
            && viewModel.client != nil
    }

    @ViewBuilder
    var messagesDestinationContent: some View {
        if isModmailMessagesRoute {
            NativeListSurface(viewModel: viewModel)
        } else if isSignedIn, let client = viewModel.client {
            DirectMessagesSurface(
                client: client,
                routeMatch: viewModel.routeMatch,
                currentUserId: currentUserId
            )
        } else {
            NativeListSurface(viewModel: viewModel)
        }
    }

    @ViewBuilder
    var signedInMessagesContent: some View {
        if let client = viewModel.client {
            VStack(alignment: .leading, spacing: Spacing.md) {
                NativeRouteDestinationHeader(entry: entry)
                    .padding([.horizontal, .top], Spacing.md)
                DirectMessagesSurface(
                    client: client,
                    routeMatch: viewModel.routeMatch,
                    currentUserId: currentUserId
                )
            }
        }
    }

    var membershipGrantContent: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            NativeRouteDestinationHeader(entry: entry)
                .padding([.horizontal, .top], Spacing.md)
            MembershipGrantSurface(client: viewModel.client)
        }
    }

    @ViewBuilder
    var membershipGrantOrUserAdminContent: some View {
        if isMembershipGrantRoute {
            membershipGrantContent
        } else {
            userAdminContent
        }
    }

    var userAdminContent: some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            NativeRouteDestinationHeader(entry: entry)
                .padding([.horizontal, .top], Spacing.md)
            IdentityVerificationAttemptGrantSurface(
                client: viewModel.client,
                idOrUsername: viewModel.routeMatch?.param("idOrUsername") ?? viewModel.routeEntityId,
                isAdministrator: isAdministrator
            )
        }
    }
}
