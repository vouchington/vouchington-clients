public enum PostContentPresentation: Equatable, Sendable {
    case content
    case removedForAuthor
    case underReviewForStaff
    case unavailable
}

public extension Post {
    /// Clearance is a coarse display state. Public eligibility also depends on server-owned context.
    func contentPresentation(viewerId: String?, isStaffViewer: Bool = false) -> PostContentPresentation {
        guard clearanceStatus == .rejected else { return .content }
        if let viewerId, !viewerId.isEmpty, viewerId == createdById { return .removedForAuthor }
        if isStaffViewer { return .underReviewForStaff }
        return .unavailable
    }

    /// Web keeps media visible to staff even when the staff member is also the author.
    func canShowAuthoredMedia(isStaffViewer: Bool = false) -> Bool {
        clearanceStatus != .rejected || isStaffViewer
    }
}
