import SwiftUI
import ViewInspector
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SpendingCategoriesRouteAndSurfaceTests: XCTestCase {
    func testSpendingCategoriesHasDedicatedNativeDestination() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/spending-categories"))
        XCTAssertEqual(route.entry.destinationIdentifier, .spendingCategories)
        XCTAssertFalse(NativeRouteDestinationIdentifier.spendingCategories.supportsRemoteNativeSurface)
        XCTAssertFalse(
            try XCTUnwrap(
                NativeRouteCatalog.includedEntries.first {
                    $0.destinationIdentifier == .profileSettings
                }
            ).patterns.contains { $0.template == "/my/spending-categories" }
        )
    }

    func testReadOnlyHouseholdRowDoesNotOfferMutations() throws {
        let category = try SpendingCategory(
            id: "entry-1",
            spendingCategoryId: "topic-1",
            amount: Money(amount: 1_250, currency: "usd"),
            spendingFrequency: .monthly,
            note: nil,
            ownerType: .household,
            canManage: false,
            spendingCategory: .init(id: "topic-1", name: "Groceries", slug: "groceries")
        )
        let viewModel = SpendingCategoriesViewModel(service: SpendingCategoryRouteStub())
        let inspected = try SpendingCategoryRow(category: category, viewModel: viewModel)
            .environment(\.locale, Locale(identifier: "en_US"))
            .inspect()
        let renderedTexts = try inspected.findAll(ViewType.Text.self).map { try $0.string() }

        XCTAssertNoThrow(try inspected.find(text: "Household (read only)"))
        XCTAssertTrue(
            renderedTexts.contains { $0.contains("12.50") && $0.contains("$") },
            "Rendered texts: \(renderedTexts)"
        )
        XCTAssertThrowsError(try inspected.find(button: "Edit"))
        XCTAssertThrowsError(try inspected.find(button: "Remove"))
    }
}

@MainActor
private final class SpendingCategoryRouteStub: SpendingCategoryServicing {
    func categories(after _: String?) async throws -> SpendingCategoryPage {
        .init(results: [])
    }

    func searchTopics(query _: String) async throws -> [TopicSearchResult] {
        []
    }

    func create(body _: CreateSpendingCategoryBody) async throws -> SpendingCategory {
        throw StubError.expected
    }

    func update(
        id _: String,
        body _: UpdateSpendingCategoryBody
    ) async throws -> SpendingCategory {
        throw StubError.expected
    }

    func delete(id _: String) async throws {}
}

private enum StubError: Error { case expected }
