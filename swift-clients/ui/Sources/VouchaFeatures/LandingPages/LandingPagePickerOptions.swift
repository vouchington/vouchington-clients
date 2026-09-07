import Foundation
import VouchaLocalization

public enum LandingPageAddType: String, CaseIterable, Identifiable, Sendable {
    case link
    case profileLink
    case review
    case referralLink
    case topicGroup

    public var id: String {
        rawValue
    }

    var titleKey: UiMessageKey {
        switch self {
        case .link: .nativeSwiftLandingPagesLink
        case .profileLink: .nativeSwiftLandingPagesProfileLink
        case .review: .nativeSwiftLandingPagesReview
        case .referralLink: .nativeSwiftLandingPagesReferralLink
        case .topicGroup: .nativeSwiftLandingPagesTopicGroup
        }
    }
}

public struct LandingPagePickerChoice: Identifiable, Equatable, Sendable {
    public let id: String
    public let label: UiVerbatimText
}

extension LandingPagesViewModel {
    public var profileLinkChoices: [LandingPagePickerChoice] {
        availableProfileLinks.map { LandingPagePickerChoice(id: $0.id, label: $0.pickerLabel) }
    }

    public var reviewChoices: [LandingPagePickerChoice] {
        availableReviews.map { LandingPagePickerChoice(id: $0.id, label: $0.pickerLabel) }
    }

    public var referralLinkChoices: [LandingPagePickerChoice] {
        availableReferralLinks.map { LandingPagePickerChoice(id: $0.id, label: $0.pickerLabel) }
    }

    public var topicChoices: [LandingPagePickerChoice] {
        availableTopics.map { LandingPagePickerChoice(id: $0.id, label: .userContent($0.name)) }
    }

    public var groupReviewChoices: [LandingPagePickerChoice] {
        availableGroupReviews.map { LandingPagePickerChoice(id: $0.id, label: $0.pickerLabel) }
    }

    public var groupReferralLinkChoices: [LandingPagePickerChoice] {
        availableGroupReferralLinks.map { LandingPagePickerChoice(id: $0.id, label: $0.pickerLabel) }
    }

    var availableProfileLinks: [LandingPageProfileLink] {
        let used = usedCandidateIDs.profileLinks
        return candidates.profileLinks.filter { !used.contains($0.id) }
    }

    var availableReviews: [LandingPageReview] {
        let used = usedCandidateIDs.reviews
        return candidates.reviews.filter { !used.contains($0.id) }
    }

    var availableReferralLinks: [LandingPageReferralLink] {
        let used = usedCandidateIDs.referralLinks
        return candidates.referralLinks.filter { !used.contains($0.id) }
    }

    var availableTopics: [LandingPageTopic] {
        let used = usedCandidateIDs.topics
        var topics: [String: LandingPageTopic] = [:]
        for review in candidates.reviews {
            for rating in review.reviewTopicRatings {
                topics[rating.topicId] = LandingPageTopic(
                    id: rating.topicId,
                    name: rating.topicName,
                    slug: rating.topicSlug,
                    topicType: "topic"
                )
            }
        }
        for referralLink in candidates.referralLinks {
            topics[referralLink.referralProgramId] = LandingPageTopic(
                id: referralLink.referralProgramId,
                name: referralLink.referralProgramName,
                slug: referralLink.referralProgramSlug,
                topicType: "referral_program"
            )
        }
        return topics.values
            .filter { !used.contains($0.id) }
            .sorted {
                let nameOrder = $0.name.localizedCaseInsensitiveCompare($1.name)
                return nameOrder == .orderedSame ? $0.id < $1.id : nameOrder == .orderedAscending
            }
    }

    var availableGroupReviews: [LandingPageReview] {
        guard let selectedTopicID else { return [] }
        let used = usedCandidateIDs.reviews
        return candidates.reviews.filter { review in
            !used.contains(review.id) && review.reviewTopicRatings.contains { $0.topicId == selectedTopicID }
        }
    }

    var availableGroupReferralLinks: [LandingPageReferralLink] {
        guard let selectedTopicID else { return [] }
        let used = usedCandidateIDs.referralLinks
        return candidates.referralLinks.filter {
            !used.contains($0.id) && $0.referralProgramId == selectedTopicID
        }
    }

    private var usedCandidateIDs: LandingPageUsedCandidateIDs {
        draftItems.reduce(into: LandingPageUsedCandidateIDs()) { used, item in
            switch item {
            case let .profileLink(_, profileLink):
                used.profileLinks.insert(profileLink.id)
            case let .review(_, review):
                used.reviews.insert(review.id)
            case let .referralLink(_, referralLink):
                used.referralLinks.insert(referralLink.id)
            case let .topicGroup(_, topic, entries):
                used.topics.insert(topic.id)
                for entry in entries {
                    switch entry {
                    case let .review(_, review): used.reviews.insert(review.id)
                    case let .referralLink(_, referralLink): used.referralLinks.insert(referralLink.id)
                    }
                }
            case .link:
                break
            }
        }
    }
}

private struct LandingPageUsedCandidateIDs {
    var profileLinks: Set<String> = []
    var reviews: Set<String> = []
    var referralLinks: Set<String> = []
    var topics: Set<String> = []
}

private extension LandingPageProfileLink {
    var pickerLabel: UiVerbatimText {
        if let label = firstNonempty(name, handle, url) {
            return .userContent(label)
        }
        return .message(.nativeSwiftLandingPagesProfileLink)
    }
}

private extension LandingPageReview {
    var pickerLabel: UiVerbatimText {
        landingPageReviewDisplay.text
    }
}

private extension LandingPageReferralLink {
    var pickerLabel: UiVerbatimText {
        if let label = firstNonempty(label, referralProgramName) {
            return .userContent(label)
        }
        return .message(.nativeSwiftLandingPagesReferralLink)
    }
}

private func firstNonempty(_ values: String?...) -> String? {
    values.lazy.compactMap { value in
        let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        return trimmed.isEmpty ? nil : trimmed
    }.first
}
