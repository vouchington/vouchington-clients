import ViewInspector
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class LandingPagesViewTests: XCTestCase {
    func testRendersCreateAndPagesSectionsWithoutSelection() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.pages = [
            makePage(id: "page-1", title: "Home", subtitle: "Public profile", slug: "home", isDefault: true),
            makePage(id: "page-2", title: "Docs", subtitle: nil, slug: "docs", isDefault: false)
        ]

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Create landing page"))
        XCTAssertEqual(try sut.inspect().find(text: "Home").string(), "Home")
        XCTAssertEqual(
            try sut.inspect().find(text: "home · Default · Public profile").string(),
            "home · Default · Public profile"
        )
        XCTAssertEqual(try sut.inspect().find(text: "Docs").string(), "Docs")
        XCTAssertThrowsError(try sut.inspect().find(button: "Make default"))
        XCTAssertThrowsError(try sut.inspect().find(button: "Save page details"))
    }

    func testDisablesCreateWhenCandidatesDisallowIt() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.candidates = LandingPageCandidates(
            canCreateLandingPages: false,
            profileLinks: [],
            reviews: [],
            referralLinks: []
        )

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertTrue(try sut.inspect().find(button: "Create landing page").isDisabled())
    }

    func testRendersSelectedPageDetailsContentAndErrorMessage() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.pages = [makePage(
            id: "page-1",
            title: "Home",
            subtitle: "Public profile",
            slug: "home",
            isDefault: false
        )]
        viewModel.setActivePage(
            makeDetail(
                id: "page-1",
                title: "Home",
                subtitle: "Public profile",
                slug: "home",
                isDefault: false,
                items: [
                    .link(id: "draft-1", label: "Newsletter", url: "https://example.com/newsletter")
                ]
            )
        )
        viewModel.errorMessage = .verbatim("Could not save landing page")

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Save page details"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Make default"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save content"))
        XCTAssertEqual(try sut.inspect().find(text: "Newsletter").string(), "Newsletter")
        XCTAssertEqual(
            try sut.inspect().find(text: "Could not save landing page").string(),
            "Could not save landing page"
        )
    }

    func testHidesMakeDefaultForDefaultPage() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.setActivePage(
            makeDetail(
                id: "page-1",
                title: "Home",
                subtitle: nil,
                slug: "home",
                isDefault: true,
                items: []
            )
        )

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertThrowsError(try sut.inspect().find(button: "Make default"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Delete"))
    }

    func testDisablesMutationButtonsWhileLoading() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.setActivePage(
            makeDetail(
                id: "page-1",
                title: "Home",
                subtitle: nil,
                slug: "home",
                isDefault: false,
                items: [.link(id: "draft-1", label: "Newsletter", url: "https://example.com/newsletter")]
            )
        )
        viewModel.state = .loading

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertTrue(try sut.inspect().find(button: "Save page details").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "Delete").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "Save content").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "Add link").isDisabled())
        XCTAssertTrue(try sut.inspect().find(button: "Remove").isDisabled())
    }

    func testDisablesPageSelectionWhileLoading() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.pages = [
            makePage(id: "page-1", title: "Home", subtitle: nil, slug: "home", isDefault: true)
        ]
        viewModel.state = .loading

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertTrue(try sut.inspect().find(button: "Home").isDisabled())
    }

    private func makePage(
        id: String,
        title: String,
        subtitle: String?,
        slug: String,
        isDefault: Bool
    ) -> LandingPage {
        LandingPage(
            id: id,
            userId: "user-1",
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100)
        )
    }

    private func makeDetail(
        id: String,
        title: String,
        subtitle: String?,
        slug: String,
        isDefault: Bool,
        items: [LandingPageItem]
    ) -> LandingPageWithItems {
        LandingPageWithItems(
            id: id,
            userId: "user-1",
            title: title,
            subtitle: subtitle,
            slug: slug,
            isDefault: isDefault,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100),
            items: items
        )
    }
}
