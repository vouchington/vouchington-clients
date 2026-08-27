import VouchaAPI

struct ModerationReportsSurfaceIdentity: Hashable {
    let isSignedIn: Bool
    let viewerTier: ModerationReportsViewerTier
    private let clientIdentifier: ObjectIdentifier?

    init(isSignedIn: Bool, client: APIClient?, viewerTier: ModerationReportsViewerTier) {
        self.isSignedIn = isSignedIn
        self.viewerTier = viewerTier
        clientIdentifier = client.map(ObjectIdentifier.init)
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(isSignedIn)
        hasher.combine(viewerTier)
        hasher.combine(clientIdentifier)
    }
}

extension NativeRouteDestinationSurface {
    var moderationReportsViewerTier: ModerationReportsViewerTier {
        if isAdministrator {
            return .administrator
        }
        return isSiteModerator ? .siteModerator : .member
    }

    var moderationReportsClient: APIClient? {
        Self.resolvedClient(entry: entry, client: client, isSignedIn: isSignedIn)
    }

    var moderationReportsSurfaceIdentity: ModerationReportsSurfaceIdentity {
        ModerationReportsSurfaceIdentity(
            isSignedIn: isSignedIn,
            client: moderationReportsClient,
            viewerTier: moderationReportsViewerTier
        )
    }
}
