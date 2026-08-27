@testable import VouchaAPI
import VouchaModels

extension ApiFixtureEndpointCoverageTests {
    static let spendingCategoryFixtureEndpoints: [String: Endpoint] = [
        "native.spending-category-topics.search.default": .spendingCategoryTopics(query: "Groceries"),
        "native.spending-categories.empty": .spendingCategories(),
        "native.spending-categories.page-1": .spendingCategories(limit: 1),
        "native.spending-categories.owned-household": .spendingCategories(
            after: spendingCategoryOwnedHouseholdCursor,
            limit: 1
        ),
        "native.spending-categories.page-2": .spendingCategories(
            after: spendingCategoryPageTwoCursor,
            limit: 1
        ),
        "native.spending-categories.create.default": .createSpendingCategory(body: createSpendingCategoryBody),
        "native.spending-categories.update.clear-note": .updateSpendingCategory(
            id: firstSpendingCategoryId,
            body: clearSpendingCategoryNoteBody
        ),
        "native.spending-categories.delete.default": .deleteSpendingCategory(id: firstSpendingCategoryId)
    ]

    static let firstSpendingCategoryId = "00000000-0000-7000-8000-000000000742"
    static let spendingCategoryOwnedHouseholdCursor =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MiIsInNjb3BlIjoibXktc3BlbmRpbmctY2F0ZWdvcmllczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA3NDA6aWQtYXNjIn0"
    static let spendingCategoryPageTwoCursor =
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MyIsInNjb3BlIjoibXktc3BlbmRpbmctY2F0ZWdvcmllczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDA3NDA6aWQtYXNjIn0"
    static let createSpendingCategoryBody = CreateSpendingCategoryBody(
        spendingCategoryId: "00000000-0000-7000-8000-000000000741",
        amount: try! Money(amount: 42_550, currency: "usd"),
        spendingFrequency: .monthly,
        note: "Family groceries"
    )
    static let clearSpendingCategoryNoteBody = UpdateSpendingCategoryBody(
        amount: try! Money(amount: 42_550, currency: "usd"),
        note: .null
    )
}
