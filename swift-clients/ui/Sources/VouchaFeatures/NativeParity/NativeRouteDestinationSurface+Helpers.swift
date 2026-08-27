import Foundation
import SwiftUI
import VouchaAPI
import VouchaDesignSystem

extension NativeRouteDestinationSurface {
    var isSignedOutChatOrSupportRoute: Bool {
        !isSignedIn && (entry.destinationIdentifier == .chat || entry.destinationIdentifier == .support)
    }

    var signedOutChatSupportContent: some View {
        EmptyStateView(
            icon: "person.crop.circle.badge.exclamationmark",
            title: .message(.nativeSwiftEmptyStateSignInRequired),
            message: .message(.nativeSwiftEmptyStateSignInChatSupportMessage)
        )
        .padding(Spacing.md)
    }

    var routeSearchQuery: String? {
        guard entry.destinationIdentifier == .webSearch || entry.destinationIdentifier == .fediverseSearch else {
            return nil
        }
        return routeQueryItems["q"] ?? routeQueryItems["query"]
    }

    var growthRange: GrowthRange {
        guard entry.destinationIdentifier == .growthDashboard else { return .thirtyDays }
        let value = routeQueryItems["range"]
        return value.flatMap(GrowthRange.init(rawValue:)) ?? .thirtyDays
    }

    var composeCommunityIdOrSlug: String? {
        viewModel.routeMatch?.param("slug") ?? routeQueryItems["community"]
    }

    private var routeQueryItems: [String: String] {
        nativeRouteQueryItems(fromEncodedQuery: routeQuery)
    }

    var topicRecommendationFormMode: String? {
        guard entry.destinationIdentifier == .topicRecommendations else { return nil }
        if viewModel.routeMatch?.path == "/topic-recommendations/create" {
            return "create"
        }
        if viewModel.routeMatch?.path.hasSuffix("/edit") == true {
            return "edit"
        }
        return nil
    }

    var isOwnerLandingPageManagementRoute: Bool {
        guard entry.destinationIdentifier == .landingPages else { return false }
        let path = viewModel.routeMatch?.path ?? entry.representativePath
        return path == "/my/landing-pages" || viewModel.routeMatch?.template == "/my/landing-page/:slug"
    }

    var ownerLandingPageSlug: String? {
        guard isOwnerLandingPageManagementRoute else { return nil }
        return viewModel.routeMatch?.param("slug")
    }

    var authLoadTaskId: String {
        [
            entry.destinationIdentifier?.rawValue ?? entry.auditFamily,
            routeMatch?.path ?? "",
            routeQuery ?? "",
            String(isSignedIn)
        ].joined(separator: "|")
    }

    var isDedicatedStaffAppealsRoute: Bool {
        entry.destinationIdentifier == .moderationAppeals && routeMatch?.path == "/appeals"
    }

    var isDedicatedStaffDisputesRoute: Bool {
        entry.destinationIdentifier == .moderationDisputes && routeMatch?.path == "/disputes"
    }

    var isDedicatedIntegrityRoute: Bool {
        entry.destinationIdentifier == .moderationIntegrity
            && IntegrityRouteKind(path: routeMatch?.path) != nil
    }

    var shouldLoadRouteSurfaceContent: Bool {
        if isDedicatedMemberAppealsRoute || isDedicatedStaffAppealsRoute
            || isDedicatedStaffDisputesRoute || isDedicatedIntegrityRoute
            || entry.destinationIdentifier == .engineeringAgents {
            return false
        }
        return switch entry.destinationIdentifier {
        case .referrals, .communitiesBrowse, .communityDetail, .chat, .support, .growthDashboard, .membershipGrants,
             .userAdmin:
            false
        case .engineeringQueues, .engineeringPostgresql, .engineeringValkey, .engineeringAiCosts,
             .moderationReviewQueue:
            false
        case .moderationReports:
            false
        case .messages:
            !isSignedInMessagesRoute
        default:
            true
        }
    }

    var isTagManagementRoute: Bool {
        guard let path = viewModel.routeMatch?.path else { return false }
        return path.contains("/tags/")
    }

    var isAdminOnlyPostComposeRoute: Bool {
        switch viewModel.routeMatch?.path {
        case "/articles/create", "/blog/create":
            true
        default:
            false
        }
    }

    var tagManagementSubjectKind: NativeTagManagementSubjectKind {
        switch entry.destinationIdentifier {
        case .rssFeedItemDetail:
            .rssFeedItem
        case .postDetail:
            .post
        case .topicDetail, .sourceDetail:
            .topic
        default:
            .topic
        }
    }

    static func resolvedClient(entry: NativeRouteCatalogEntry, client: APIClient?, isSignedIn: Bool) -> APIClient? {
        let requiresAuth = entry.destinationIdentifier == .urlDetail
            || entry.destinationIdentifier == .urlsBrowse
            || entry.destinationIdentifier == .messages
            || entry.destinationIdentifier == .referrals
            || entry.destinationIdentifier == .chat
            || entry.destinationIdentifier == .support
            || entry.destinationIdentifier == .engineeringQueues
            || entry.destinationIdentifier == .engineeringPostgresql
            || entry.destinationIdentifier == .engineeringValkey
            || entry.destinationIdentifier == .engineeringAiCosts
            || entry.destinationIdentifier == .engineeringAgents
            || entry.destinationIdentifier == .moderationReports
            || entry.destinationIdentifier == .moderationDisputes
            || entry.destinationIdentifier == .moderationIntegrity
        return requiresAuth && !isSignedIn ? nil : client
    }
}
