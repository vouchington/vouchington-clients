import Foundation
import VouchaAPI
import VouchaModels
import XCTest

final class MoneyAndCurrencyTests: XCTestCase {
    func testDecodesMoneyBoundariesAndRejectsInvalidContracts() throws {
        let decoder = makeVouchaDecoder()
        XCTAssertEqual(
            try decoder.decode(Money.self, from: Data(#"{"amount":9007199254740991,"currency":"usd"}"#.utf8)),
            try Money(amount: Money.maximumAmount, currency: "usd")
        )
        for json in [
            #"{"amount":-1,"currency":"usd"}"#,
            #"{"amount":9007199254740992,"currency":"usd"}"#,
            #"{"amount":1,"currency":"USD"}"#
        ] {
            XCTAssertThrowsError(try decoder.decode(Money.self, from: Data(json.utf8)))
        }
    }

    func testScaledMoneyRequiresLiteralScaleSix() throws {
        let valid = try makeVouchaDecoder().decode(
            ScaledMoney.self,
            from: Data(#"{"amount":35000,"currency":"usd","scale":6}"#.utf8)
        )
        XCTAssertEqual(valid.majorUnitDecimal, Decimal(string: "0.035"))
        XCTAssertThrowsError(try makeVouchaDecoder().decode(
            ScaledMoney.self,
            from: Data(#"{"amount":35000,"currency":"usd","scale":5}"#.utf8)
        ))
    }

    func testScaledMoneyAggregatePreservesArbitraryCanonicalDigits() throws {
        let aggregate = try makeVouchaDecoder().decode(
            ScaledMoneyAggregate.self,
            from: Data(
                #"{"amount":"180143985094819820000","currency":"usd","scale":6}"#.utf8
            )
        )
        XCTAssertEqual(aggregate.majorUnitString, "180143985094819.82")
        for amount in ["", "01", "-1", "1.5"] {
            XCTAssertThrowsError(try ScaledMoneyAggregate(amount: amount, currency: "usd"))
        }
    }

    func testMoneyRangeRequiresOneCurrencyAndIncreasingBounds() throws {
        let minimum = try Money(amount: 100, currency: "usd")
        XCTAssertNoThrow(try MoneyRange(minimum: minimum, maximum: nil))
        XCTAssertThrowsError(try MoneyRange(
            minimum: minimum,
            maximum: Money(amount: 200, currency: "jpy")
        ))
        XCTAssertThrowsError(try MoneyRange(
            minimum: minimum,
            maximum: Money(amount: 100, currency: "usd")
        ))
    }

    func testCurrencyFixtureAndEndpointUseCursorContract() throws {
        let page = try makeVouchaDecoder().decode(
            CurrencyPage.self,
            from: ApiFixtureLoader.data("shared.currencies.list.default")
        )
        XCTAssertEqual(page.results.first, try Currency(code: "aud", minorUnitExponent: 2))
        XCTAssertEqual(page.results.first(where: { $0.code == "jpy" })?.minorUnitExponent, 0)
        let endpoint = Endpoint.currencies(after: "currency cursor", limit: 6)
        XCTAssertEqual(endpoint.path, "/api/v1/currencies")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "6"),
            URLQueryItem(name: "after", value: "currency cursor")
        ])
    }

    func testSupportedCurrencyCatalogIsCanonical() {
        XCTAssertEqual(Currency.supported.map(\.code), ["aud", "cad", "eur", "gbp", "jpy", "usd"])
        XCTAssertEqual(Currency.supported.map(\.minorUnitExponent), [2, 2, 2, 2, 0, 2])
    }
}
