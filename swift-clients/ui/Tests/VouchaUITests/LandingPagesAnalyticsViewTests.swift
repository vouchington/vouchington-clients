import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class LandingPagesAnalyticsViewTests: XCTestCase {
    func testRendersLocalizedAnalyticsUpgradeState() throws {
        let viewModel = LandingPagesViewModel(client: nil, canViewAnalytics: false)
        viewModel.setActivePage(makePageDetail())

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertEqual(
            try sut.inspect().find(text: "Upgrade to Plus or Pro to view landing page analytics.").string(),
            "Upgrade to Plus or Pro to view landing page analytics."
        )
    }

    func testRendersLoadedAnalyticsRows() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.pages = [makePage()]
        viewModel.setActivePage(makePageDetail())
        viewModel.selectedPageAnalytics = LandingPageAnalytics(
            totalVisits: 18,
            totalClicks: 6,
            uniqueVisitors: 9,
            ctr: 0.33,
            itemClicks: [],
            dailyStats: [],
            utmSources: [],
            conversionFunnel: .init(
                totalVisits: 18,
                totalClicks: 6,
                totalSignups: 2,
                visitToClickRate: 0.33
            )
        )
        viewModel.selectedPageAnalyticsState = .loaded

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "Visits").string(), "Visits")
        XCTAssertEqual(try sut.inspect().find(text: "18 visits").string(), "18 visits")
        XCTAssertEqual(try sut.inspect().find(text: "6 clicks").string(), "6 clicks")
        XCTAssertEqual(try sut.inspect().find(text: "9 visitors").string(), "9 visitors")
    }

    func testRendersAnalyticsLoadingState() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.pages = [makePage()]
        viewModel.setActivePage(makePageDetail())
        viewModel.selectedPageAnalyticsState = .loading

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "Loading analytics").string(), "Loading analytics")
        XCTAssertNoThrow(try sut.inspect().find(ViewType.ProgressView.self))
    }

    func testRendersAnalyticsErrorStateWithRetry() throws {
        let viewModel = LandingPagesViewModel(client: nil)
        viewModel.pages = [makePage()]
        viewModel.setActivePage(makePageDetail())
        viewModel.selectedPageAnalyticsState = .error(.api(statusCode: 500, preconditionCode: nil))

        let sut = LandingPagesView(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "An error occurred.").string(), "An error occurred.")
        XCTAssertNoThrow(try sut.inspect().find(button: "Try Again"))
    }

    private func makePage() -> LandingPage {
        LandingPage(
            id: "page-1",
            userId: "user-1",
            title: "Home",
            subtitle: "Public profile",
            slug: "home",
            isDefault: true,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100)
        )
    }

    private func makePageDetail() -> LandingPageWithItems {
        LandingPageWithItems(
            id: "page-1",
            userId: "user-1",
            title: "Home",
            subtitle: "Public profile",
            slug: "home",
            isDefault: true,
            createdAt: Date(timeIntervalSince1970: 1_717_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_717_000_100),
            items: []
        )
    }
}
