import Foundation
import VouchaModels

extension CommunityDetailViewModel {
    var shouldSkipInviteRows: Bool {
        let role = summary.isMember ? communityDetail?.membership?.role : nil
        return summary.isMember && role != .owner && role != .moderator
    }

    func listItemIcon(for itemType: CommunityListItemType) -> String {
        switch itemType {
        case .topic: "tag"
        case .rssFeed: "dot.radiowaves.left.and.right"
        case .post: "doc.text"
        case .urlHostname, .url: "link"
        }
    }

    func joinedDetailParts(_ parts: [String?]) -> String {
        parts.compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
            .joined(separator: " · ")
    }
}
