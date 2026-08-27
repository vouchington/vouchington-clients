import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class LandingPagePickerOptionsTests: XCTestCase {
    func testChoicesUseHumanLabelsAndExcludeStandaloneAndNestedCandidates() throws {
        let viewModel = try makeViewModel()
        let detail = try decodeDetail()
        viewModel.setActivePage(detail)
        viewModel.candidates = try decodeCandidates()

        XCTAssertTrue(viewModel.profileLinkChoices.isEmpty)
        XCTAssertTrue(viewModel.reviewChoices.isEmpty)
        XCTAssertTrue(viewModel.referralLinkChoices.isEmpty)

        viewModel.setActivePage(detail.withItems([]))
        XCTAssertEqual(labels(viewModel.profileLinkChoices), ["Website"])
        XCTAssertEqual(labels(viewModel.reviewChoices), ["Best Travel Card", "Travel Card Benefits"])
        XCTAssertEqual(labels(viewModel.referralLinkChoices), ["Apply", "Learn more"])
        XCTAssertFalse(labels(viewModel.profileLinkChoices + viewModel.reviewChoices).contains { $0.contains("-1") })
    }

    func testTopicsAreDeduplicatedSortedAndExcludeUsedGroups() throws {
        let viewModel = try makeViewModel()
        try viewModel.setActivePage(decodeDetail().withItems([]))
        let candidates = try decodeCandidates()
        let alphaReview = VouchaFeatures.LandingPageReview(
            id: "review-alpha",
            title: "Alpha review",
            slug: nil,
            markdown: "Alpha",
            createdAt: Date(timeIntervalSince1970: 1),
            reviewTopicRatings: [VouchaFeatures.LandingPageReviewTopicRating(
                topicId: "topic-alpha",
                topicName: "Alpha",
                topicSlug: "alpha",
                rating: 5,
                orderIndex: 0
            )]
        )
        viewModel.candidates = VouchaFeatures.LandingPageCandidates(
            canCreateLandingPages: true,
            profileLinks: candidates.profileLinks,
            reviews: candidates.reviews + [alphaReview],
            referralLinks: candidates.referralLinks
        )

        XCTAssertEqual(labels(viewModel.topicChoices), ["Alpha", "Travel Cards"])
        XCTAssertEqual(viewModel.topicChoices.map(\.id), ["topic-alpha", "topic-1"])

        viewModel.addType = .topicGroup
        viewModel.selectedTopicID = "topic-1"
        viewModel.selectedGroupReviewIDs = ["review-1"]
        XCTAssertTrue(viewModel.addSelectedItem())
        XCTAssertEqual(viewModel.topicChoices.map(\.id), ["topic-alpha"])
    }

    func testAddsEveryCandidateTypeAndBuildsGroupInCandidateOrder() throws {
        let viewModel = try makeViewModel()
        try viewModel.setActivePage(decodeDetail().withItems([]))
        viewModel.candidates = try decodeCandidates()

        viewModel.addType = .profileLink
        viewModel.selectedCandidateID = "profile-link-1"
        XCTAssertTrue(viewModel.addSelectedItem())
        viewModel.addType = .review
        viewModel.selectedCandidateID = "review-1"
        XCTAssertTrue(viewModel.addSelectedItem())
        viewModel.addType = .referralLink
        viewModel.selectedCandidateID = "referral-link-1"
        XCTAssertTrue(viewModel.addSelectedItem())
        viewModel.addType = .topicGroup
        viewModel.selectedTopicID = "topic-1"
        viewModel.selectedGroupReviewIDs = ["review-2"]
        viewModel.selectedGroupReferralLinkIDs = ["referral-link-2"]
        XCTAssertTrue(viewModel.addSelectedItem())
        viewModel.addType = .link
        viewModel.linkLabel = " Newsletter "
        viewModel.linkUrl = " https://example.com/newsletter "
        XCTAssertTrue(viewModel.addSelectedItem())

        XCTAssertEqual(viewModel.draftItems.map(\.input), expectedAllItemInputs)
        guard case let .topicGroup(_, _, entries) = viewModel.draftItems[3] else {
            return XCTFail("Expected topic group")
        }
        XCTAssertEqual(entries.map(\.candidateID), ["review-2", "referral-link-2"])
    }

    func testAddRevalidatesSelectionAndManualLinkValidation() throws {
        let viewModel = try makeViewModel()
        try viewModel.setActivePage(decodeDetail().withItems([]))
        viewModel.candidates = try decodeCandidates()
        viewModel.addType = .profileLink
        viewModel.selectedCandidateID = "missing-profile"
        XCTAssertFalse(viewModel.canAddItem)
        XCTAssertFalse(viewModel.addSelectedItem())

        viewModel.addType = .link
        viewModel.linkLabel = "Docs"
        viewModel.linkUrl = "https://example.com/docs#private"
        XCTAssertFalse(viewModel.canAddItem)
        viewModel.linkUrl = "ftp://example.com/docs"
        XCTAssertFalse(viewModel.canAddItem)
        viewModel.linkUrl = "https://example.com/docs"
        XCTAssertTrue(viewModel.canAddItem)
    }

    func testRemoveAndMoveUpdateAvailabilityAndOrderedDraft() throws {
        let viewModel = try makeViewModel()
        try viewModel.setActivePage(decodeDetail().withItems([]))
        viewModel.candidates = try decodeCandidates()
        viewModel.addType = .profileLink
        viewModel.selectedCandidateID = "profile-link-1"
        viewModel.addSelectedItem()
        viewModel.addType = .review
        viewModel.selectedCandidateID = "review-1"
        viewModel.addSelectedItem()

        let profileItemID = viewModel.draftItems[0].id
        let reviewItemID = viewModel.draftItems[1].id
        viewModel.moveItem(id: reviewItemID, direction: -1)
        XCTAssertEqual(viewModel.draftItems.map(\.id), [reviewItemID, profileItemID])
        viewModel.removeItem(id: profileItemID)
        XCTAssertEqual(viewModel.profileLinkChoices.map(\.id), ["profile-link-1"])
    }

    func testCandidateRefreshOnlyReconcilesTransientSelections() throws {
        let viewModel = try makeViewModel()
        try viewModel.setActivePage(decodeDetail().withItems([]))
        let candidates = try decodeCandidates()
        viewModel.candidates = candidates
        viewModel.addType = .profileLink
        viewModel.selectedCandidateID = "profile-link-1"
        viewModel.addSelectedItem()
        viewModel.addType = .review
        viewModel.selectedCandidateID = "review-2"
        let preservedDraft = viewModel.draftItems

        viewModel.candidates = .empty
        viewModel.reconcilePickerSelections()

        XCTAssertEqual(viewModel.draftItems, preservedDraft)
        XCTAssertNil(viewModel.selectedCandidateID)

        let newReview = VouchaFeatures.LandingPageReview(
            id: "review-new",
            title: "New travel review",
            slug: nil,
            markdown: "New review",
            createdAt: Date(timeIntervalSince1970: 2),
            reviewTopicRatings: candidates.reviews[0].reviewTopicRatings
        )
        viewModel.candidates = VouchaFeatures.LandingPageCandidates(
            canCreateLandingPages: true,
            profileLinks: candidates.profileLinks,
            reviews: candidates.reviews + [newReview],
            referralLinks: candidates.referralLinks
        )

        XCTAssertEqual(viewModel.draftItems, preservedDraft)
        XCTAssertEqual(viewModel.reviewChoices.map(\.id), ["review-1", "review-2", "review-new"])
        XCTAssertEqual(Set(viewModel.reviewChoices.map(\.id)).count, viewModel.reviewChoices.count)
        viewModel.addType = .profileLink
        XCTAssertTrue(viewModel.profileLinkChoices.isEmpty)
    }

    private var expectedAllItemInputs: [VouchaAPI.LandingPageItemInput] {
        [
            .profileLink(id: "profile-link-1"),
            .review(id: "review-1"),
            .referralLink(id: "referral-link-1"),
            .topicGroup(
                topicId: "topic-1",
                entries: [.review(id: "review-2"), .referralLink(id: "referral-link-2")]
            ),
            .link(label: "Newsletter", url: "https://example.com/newsletter")
        ]
    }

    private func makeViewModel() throws -> LandingPagesViewModel {
        LandingPagesViewModel(client: nil)
    }

    private func decodeDetail() throws -> LandingPageWithItems {
        try decoder.decode(
            LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        ).landingPage
    }

    private func decodeCandidates() throws -> VouchaFeatures.LandingPageCandidates {
        try decoder.decode(
            VouchaFeatures.LandingPageCandidatesResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-candidates.default")
        ).candidates
    }

    private var decoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return decoder
    }

    private func labels(_ choices: [LandingPagePickerChoice]) -> [String] {
        choices.map { UiMessages.string($0.label, locale: .english) }
    }
}

private extension LandingPageWithItems {
    func withItems(_ items: [VouchaFeatures.LandingPageItem]) -> LandingPageWithItems {
        LandingPageWithItems(
            id: id,
            userId: userId,
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: createdAt,
            updatedAt: updatedAt,
            items: items
        )
    }
}

private extension VouchaFeatures.LandingPageTopicGroupEntry {
    var candidateID: String {
        switch self {
        case let .review(_, review): review.id
        case let .referralLink(_, referralLink): referralLink.id
        }
    }
}
