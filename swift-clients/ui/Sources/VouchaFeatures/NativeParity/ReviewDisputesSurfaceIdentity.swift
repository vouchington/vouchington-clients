import VouchaAPI

struct ReviewDisputesSurfaceIdentity: Hashable {
    let isSignedIn: Bool
    let currentUserId: String?
    let isAdministrator: Bool
    let isSiteModerator: Bool
    private let clientIdentifier: ObjectIdentifier?

    init(
        isSignedIn: Bool,
        currentUserId: String?,
        isAdministrator: Bool,
        isSiteModerator: Bool,
        client: APIClient?
    ) {
        self.isSignedIn = isSignedIn
        self.currentUserId = currentUserId
        self.isAdministrator = isAdministrator
        self.isSiteModerator = isSiteModerator
        clientIdentifier = client.map(ObjectIdentifier.init)
    }

    func hash(into hasher: inout Hasher) {
        hasher.combine(isSignedIn)
        hasher.combine(currentUserId)
        hasher.combine(isAdministrator)
        hasher.combine(isSiteModerator)
        hasher.combine(clientIdentifier)
    }
}

extension NativeRouteDestinationSurface {
    var reviewDisputesClient: APIClient? {
        Self.resolvedClient(entry: entry, client: client, isSignedIn: isSignedIn)
    }

    var reviewDisputesSurfaceIdentity: ReviewDisputesSurfaceIdentity {
        ReviewDisputesSurfaceIdentity(
            isSignedIn: isSignedIn,
            currentUserId: currentUserId,
            isAdministrator: isAdministrator,
            isSiteModerator: isSiteModerator,
            client: reviewDisputesClient
        )
    }
}
