import Foundation
import SwiftUI
import ViewInspector
@testable import VouchaFeatures
import XCTest

@MainActor
final class LandingPagePickerViewTests: XCTestCase {
    func testRendersHumanCandidateLabelsAndEveryAddType() throws {
        let viewModel = try makeViewModel()
        viewModel.addType = .review
        let sut = landingPageContentSection(viewModel)

        for title in ["Link", "Profile link", "Review", "Referral link", "Topic group"] {
            XCTAssertNoThrow(try sut.inspect().find(text: title))
        }
        XCTAssertNoThrow(try sut.inspect().find(text: "Best Travel Card"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Travel Card Benefits"))
        XCTAssertThrowsError(try sut.inspect().find(text: "review-1"))
    }

    func testAddControlRequiresAValidSelection() throws {
        let viewModel = try makeViewModel()
        var sut = landingPageContentSection(viewModel)
        XCTAssertTrue(try sut.inspect().find(button: "Add link").isDisabled())

        viewModel.linkLabel = "Docs"
        viewModel.linkUrl = "https://example.com/docs"
        sut = landingPageContentSection(viewModel)
        XCTAssertFalse(try sut.inspect().find(button: "Add link").isDisabled())

        viewModel.addType = .profileLink
        sut = landingPageContentSection(viewModel)
        XCTAssertTrue(try sut.inspect().find(button: "Add item").isDisabled())
        viewModel.selectedCandidateID = "profile-link-1"
        sut = landingPageContentSection(viewModel)
        XCTAssertFalse(try sut.inspect().find(button: "Add item").isDisabled())
    }

    func testRendersExplicitTopicMemberControls() throws {
        let viewModel = try makeViewModel()
        viewModel.addType = .topicGroup
        viewModel.selectedTopicID = "topic-1"
        let sut = landingPageContentSection(viewModel)

        XCTAssertNoThrow(try sut.inspect().find(text: "Travel Cards"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Reviews"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Best Travel Card"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Referral links"))
        XCTAssertNoThrow(try sut.inspect().find(text: "Apply"))
        XCTAssertTrue(try sut.inspect().find(button: "Add item").isDisabled())
    }

    func testRendersLocalizedPickerChromeFromEnvironmentLocale() async throws {
        let viewModel = try makeViewModel()
        viewModel.addType = .review
        let sut = LandingPageContentSection(viewModel: viewModel, locale: Locale(identifier: "es"))

        try await ViewHosting.host(sut) {
            XCTAssertNoThrow(try sut.inspect().find(text: "Tipo de elemento"))
            XCTAssertNoThrow(try sut.inspect().find(text: "Reseña"))
            XCTAssertNoThrow(try sut.inspect().find(text: "Añadir elemento"))
        }
    }

    private func makeViewModel() throws -> LandingPagesViewModel {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        let detail = try decoder.decode(
            LandingPageWithItemsResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-detail.default")
        ).landingPage
        let candidates = try decoder.decode(
            LandingPageCandidatesResponse.self,
            from: ApiFixtureLoader.data("native.landing-page-candidates.default")
        ).candidates
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.setActivePage(detail.withNoItems())
        viewModel.candidates = candidates
        return viewModel
    }

    private func landingPageContentSection(_ viewModel: LandingPagesViewModel) -> LandingPageContentSection {
        LandingPageContentSection(viewModel: viewModel, locale: Locale(identifier: "en"))
    }
}

private extension LandingPageWithItems {
    func withNoItems() -> LandingPageWithItems {
        LandingPageWithItems(
            id: id,
            userId: userId,
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: createdAt,
            updatedAt: updatedAt,
            items: []
        )
    }
}
