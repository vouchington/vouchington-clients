import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class SpendingCategoryMutationRegressionTests: XCTestCase {
    func testDraftBuildsCreateAndMinimalUpdateBodiesAndRejectsInvalidAmounts() throws {
        let original = category("entry", amount: 1_200, note: "Keep")
        var draft = SpendingCategoryDraft(category: original, locale: Locale(identifier: "en_US"))

        XCTAssertNil(try draft.updateBody(comparedWith: original))
        XCTAssertEqual(draft.currency, "usd")

        draft.currency = "eur"
        XCTAssertEqual(
            try XCTUnwrap(try draft.updateBody(comparedWith: original)).amount,
            money(1_200, currency: "eur")
        )
        draft.currency = "usd"

        draft.frequency = .annually
        draft.note = ""
        let update = try XCTUnwrap(try draft.updateBody(comparedWith: original))
        XCTAssertNil(update.amount)
        XCTAssertEqual(update.spendingFrequency, .annually)
        XCTAssertTrue(try jsonObject(update)["note"] is NSNull)

        draft.updateAmountText("19.75")
        let create = try draft.createBody(spendingCategoryId: "topic-entry")
        XCTAssertEqual(create.spendingCategoryId, "topic-entry")
        XCTAssertEqual(create.amount, money(1_975))
        XCTAssertNil(create.note)

        draft.updateAmountText("1.234")
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))
        draft.updateAmountText("-1")
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))
        draft.updateAmountText("1e2")
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))
        draft.updateAmountText("90,071,992,547,409.91")
        XCTAssertEqual(
            try draft.createBody(spendingCategoryId: "topic-entry").amount,
            money(Money.maximumAmount)
        )
        draft.updateAmountText("90071992547409.92")
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))
        draft.updateAmountText("1")
        draft.currency = "zzz"
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))

        draft.currency = "jpy"
        draft.updateAmountText("123")
        XCTAssertEqual(
            try draft.createBody(spendingCategoryId: "topic-entry").amount,
            money(123, currency: "jpy")
        )
        draft.updateAmountText("123.4")
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))
        draft.updateAmountText("9007199254740991")
        XCTAssertEqual(
            try draft.createBody(spendingCategoryId: "topic-entry").amount,
            money(Money.maximumAmount, currency: "jpy")
        )
        draft.updateAmountText("9007199254740992")
        XCTAssertThrowsError(try draft.createBody(spendingCategoryId: "topic-entry"))

        let yenCategory = category("yen", amount: 1_250, currency: "jpy")
        let yenDraft = SpendingCategoryDraft(category: yenCategory, locale: Locale(identifier: "en_US"))
        XCTAssertEqual(yenDraft.currency, "jpy")
        XCTAssertEqual(yenDraft.amountText, "1,250")
    }

    func testDeleteFailureReconcilesConcurrentUpsertsInCanonicalOrder() async {
        let deletedCategory = category("c")
        let service = SpendingServiceStub()
        service.deleteDelay = true
        service.deleteResult = .failure(MutationTestError.expected)
        let viewModel = SpendingCategoriesViewModel(service: service)
        viewModel.categories = [category("b"), deletedCategory]

        let deletion = Task { await viewModel.delete(deletedCategory) }
        await waitUntil { service.deleteCalls == 1 }
        viewModel.upsert(category("a"))
        service.resumeDelete()

        let deleted = await deletion.value
        XCTAssertFalse(deleted)
        XCTAssertEqual(viewModel.categories.map(\.id), ["a", "b", "c"])
    }

    func testDraftRetainsTheLocaleThatParsedUserEditedAmounts() throws {
        var englishDraft = SpendingCategoryDraft(locale: Locale(identifier: "en_US"))
        englishDraft.updateAmountText("1.234")
        XCTAssertThrowsError(try englishDraft.createBody(spendingCategoryId: "topic"))
        englishDraft.updateAmountText("1.23")
        XCTAssertEqual(
            try englishDraft.createBody(spendingCategoryId: "topic").amount,
            try Money(amount: 123, currency: "usd")
        )
        englishDraft.applyLocale(Locale(identifier: "de_DE"))
        XCTAssertEqual(
            try englishDraft.createBody(spendingCategoryId: "topic").amount,
            try Money(amount: 123, currency: "usd")
        )

        var germanDraft = try SpendingCategoryDraft(
            amount: Money(amount: 150, currency: "usd"),
            locale: Locale(identifier: "en_US")
        )
        germanDraft.applyLocale(Locale(identifier: "de_DE"))
        germanDraft.updateAmountText("2,5")
        germanDraft.applyLocale(Locale(identifier: "en_US"))
        XCTAssertEqual(
            try germanDraft.createBody(spendingCategoryId: "topic").amount,
            try Money(amount: 250, currency: "usd")
        )

        var frenchDraft = SpendingCategoryDraft(locale: Locale(identifier: "fr_FR"))
        frenchDraft.updateAmountText("1 234,56")
        XCTAssertEqual(
            try frenchDraft.createBody(spendingCategoryId: "topic").amount,
            try Money(amount: 123_456, currency: "usd")
        )
    }

    private func category(
        _ id: String,
        amount: Int64 = 100,
        currency: String = "usd",
        note: String? = nil
    ) -> SpendingCategory {
        .init(
            id: id,
            spendingCategoryId: "topic-\(id)",
            amount: money(amount, currency: currency),
            spendingFrequency: .monthly,
            note: note,
            ownerType: .individual,
            canManage: true,
            spendingCategory: .init(id: "topic-\(id)", name: id, slug: id)
        )
    }

    private func money(_ amount: Int64, currency: String = "usd") -> Money {
        try! Money(amount: amount, currency: currency)
    }

    private func jsonObject(_ value: some Encodable) throws -> [String: Any] {
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        return try XCTUnwrap(
            JSONSerialization.jsonObject(with: encoder.encode(value)) as? [String: Any]
        )
    }

    private func waitUntil(_ condition: @escaping @MainActor () -> Bool) async {
        for _ in 0 ..< 200 where !condition() {
            await Task.yield()
        }
    }
}

private enum MutationTestError: Error { case expected }
