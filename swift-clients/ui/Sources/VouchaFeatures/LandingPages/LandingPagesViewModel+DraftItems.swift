import Foundation
import VouchaAPI

public extension LandingPagesViewModel {
    var canAddItem: Bool {
        guard selectedPage != nil, !isLoading else { return false }
        switch addType {
        case .link:
            let label = linkLabel.trimmingCharacters(in: .whitespacesAndNewlines)
            let url = linkUrl.trimmingCharacters(in: .whitespacesAndNewlines)
            return isValidLinkLabel(label) && isValidLinkUrl(url)
        case .profileLink:
            return availableProfileLinks.contains { $0.id == selectedCandidateID }
        case .review:
            return availableReviews.contains { $0.id == selectedCandidateID }
        case .referralLink:
            return availableReferralLinks.contains { $0.id == selectedCandidateID }
        case .topicGroup:
            guard availableTopics.contains(where: { $0.id == selectedTopicID }) else { return false }
            return availableGroupReviews.contains { selectedGroupReviewIDs.contains($0.id) }
                || availableGroupReferralLinks.contains { selectedGroupReferralLinkIDs.contains($0.id) }
        }
    }

    @discardableResult
    func addSelectedItem() -> Bool {
        guard canAddItem else { return false }
        if addType == .link {
            return addLinkItem()
        }
        guard let item = selectedDraftItem() else { return false }
        draftItems.append(item)
        clearTransientPickerSelection()
        return true
    }

    @discardableResult
    func addLinkItem() -> Bool {
        guard !isLoading else { return false }
        let label = linkLabel.trimmingCharacters(in: .whitespacesAndNewlines)
        let url = linkUrl.trimmingCharacters(in: .whitespacesAndNewlines)
        guard isValidLinkLabel(label), isValidLinkUrl(url) else { return false }
        draftItems.append(.link(id: "draft-link-\(UUID().uuidString)", label: label, url: url))
        linkLabel = ""
        linkUrl = ""
        return true
    }

    func removeItem(id: String) {
        guard !isLoading else { return }
        draftItems.removeAll { $0.id == id }
    }

    func moveItem(id: String, direction: Int) {
        guard !isLoading else { return }
        guard let index = draftItems.firstIndex(where: { $0.id == id }) else { return }
        let target = index + direction
        guard draftItems.indices.contains(target) else { return }
        draftItems.swapAt(index, target)
    }

    func reconcilePickerSelections() {
        switch addType {
        case .profileLink:
            if !availableProfileLinks.contains(where: { $0.id == selectedCandidateID }) {
                selectedCandidateID = nil
            }
        case .review:
            if !availableReviews.contains(where: { $0.id == selectedCandidateID }) {
                selectedCandidateID = nil
            }
        case .referralLink:
            if !availableReferralLinks.contains(where: { $0.id == selectedCandidateID }) {
                selectedCandidateID = nil
            }
        case .topicGroup:
            guard availableTopics.contains(where: { $0.id == selectedTopicID }) else {
                selectedTopicID = nil
                return
            }
            selectedGroupReviewIDs.formIntersection(availableGroupReviews.map(\.id))
            selectedGroupReferralLinkIDs.formIntersection(availableGroupReferralLinks.map(\.id))
        case .link:
            break
        }
    }

    func clearTransientPickerSelection() {
        selectedCandidateID = nil
        selectedTopicID = nil
        selectedGroupReviewIDs = []
        selectedGroupReferralLinkIDs = []
    }

    private func topicGroupEntries() -> [LandingPageTopicGroupEntry] {
        let reviews = availableGroupReviews.compactMap { review -> LandingPageTopicGroupEntry? in
            guard selectedGroupReviewIDs.contains(review.id) else { return nil }
            return .review(id: draftID("group-review"), review: review)
        }
        let referralLinks = availableGroupReferralLinks.compactMap { link -> LandingPageTopicGroupEntry? in
            guard selectedGroupReferralLinkIDs.contains(link.id) else { return nil }
            return .referralLink(id: draftID("group-referral-link"), referralLink: link)
        }
        return reviews + referralLinks
    }

    private func selectedDraftItem() -> LandingPageItem? {
        switch addType {
        case .profileLink:
            availableProfileLinks.first(where: { $0.id == selectedCandidateID }).map {
                .profileLink(id: draftID("profile-link"), profileLink: $0)
            }
        case .review:
            availableReviews.first(where: { $0.id == selectedCandidateID }).map {
                .review(id: draftID("review"), review: $0)
            }
        case .referralLink:
            availableReferralLinks.first(where: { $0.id == selectedCandidateID }).map {
                .referralLink(id: draftID("referral-link"), referralLink: $0)
            }
        case .topicGroup:
            selectedTopicGroupItem()
        case .link:
            nil
        }
    }

    private func selectedTopicGroupItem() -> LandingPageItem? {
        guard let topic = availableTopics.first(where: { $0.id == selectedTopicID }) else { return nil }
        let entries = topicGroupEntries()
        guard !entries.isEmpty else { return nil }
        return .topicGroup(id: draftID("topic-group"), topic: topic, entries: entries)
    }

    private func draftID(_ kind: String) -> String {
        "draft-\(kind)-\(UUID().uuidString)"
    }
}

private let maxLinkLabelLength = 100
private let maxLinkUrlLength = 2_048

private func isValidLinkLabel(_ value: String) -> Bool {
    !value.isEmpty && value.utf16.count <= maxLinkLabelLength
}

private func isValidLinkUrl(_ value: String) -> Bool {
    value.utf16.count <= maxLinkUrlLength && isHttpUrlWithoutFragment(value)
}

private func isHttpUrlWithoutFragment(_ value: String) -> Bool {
    guard let components = URLComponents(string: value),
          let scheme = components.scheme?.lowercased(),
          scheme == "http" || scheme == "https",
          components.host?.isEmpty == false,
          components.fragment == nil else {
        return false
    }
    return true
}

extension LandingPageItem {
    var input: LandingPageItemInput {
        switch self {
        case let .profileLink(_, profileLink):
            .profileLink(id: profileLink.id)
        case let .review(_, review):
            .review(id: review.id)
        case let .referralLink(_, referralLink):
            .referralLink(id: referralLink.id)
        case let .topicGroup(_, topic, entries):
            .topicGroup(topicId: topic.id, entries: entries.map(\.input))
        case let .link(_, label, url):
            .link(label: label, url: url)
        }
    }
}

private extension LandingPageTopicGroupEntry {
    var input: LandingPageTopicGroupEntryInput {
        switch self {
        case let .review(_, review):
            .review(id: review.id)
        case let .referralLink(_, referralLink):
            .referralLink(id: referralLink.id)
        }
    }
}
