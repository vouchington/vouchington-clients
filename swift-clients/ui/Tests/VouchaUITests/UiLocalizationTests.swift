import Observation
@testable import VouchaLocalization
import XCTest

@MainActor
final class UiLocalizationTests: XCTestCase {
    func testResolverUsesSavedLocaleBeforePreferredLanguages() {
        XCTAssertEqual(
            UiLocaleResolver.resolve(savedUiLocale: "fr-CA", preferredLanguages: ["pt-BR"]),
            .french
        )
    }

    func testResolverUsesFirstSupportedPreferredLanguageAndFallsBackToEnglish() {
        XCTAssertEqual(
            UiLocaleResolver.resolve(savedUiLocale: nil, preferredLanguages: ["de-DE", "es-MX"]),
            .spanish
        )
        XCTAssertEqual(
            UiLocaleResolver.resolve(savedUiLocale: "unsupported", preferredLanguages: ["de-DE"]),
            .english
        )
    }

    func testControllerRefreshesMessagesWithoutRecreation() {
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])
        XCTAssertEqual(controller.string(.commonCancel), "Cancel")
        controller.update(savedUiLocale: "fr")
        XCTAssertEqual(controller.string(.commonCancel), "Annuler")
    }

    func testControllerInterpolatesAndSelectsPluralResources() {
        let controller = UiLocaleController(savedUiLocale: "es", preferredLanguages: [])
        XCTAssertEqual(
            controller.string(
                UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 2],
                    selectedCase: "post"
                )
            ),
            "2 publicaciones"
        )
    }

    func testCardinalPluralRulesMatchSupportedLocaleCatalog() {
        let cases: [(UiLocale, Double, UiPluralCategory)] = [
            (.english, 0, .other), (.english, 0.5, .other), (.english, 1, .one), (.english, 2, .other),
            (.spanish, 0, .other), (.spanish, 0.5, .other), (.spanish, 1, .one), (.spanish, 2, .other),
            (.french, 0, .one), (.french, 0.5, .one), (.french, 1, .one), (.french, 2, .other),
            (.french, 1_000_000, .other),
            (.portuguese, 0, .one), (.portuguese, 0.5, .one), (.portuguese, 1, .one),
            (.portuguese, 2, .other), (.portuguese, 2_000_000, .other),
            (.english, .infinity, .other)
        ]

        for (locale, value, expected) in cases {
            XCTAssertEqual(UiPluralRules.cardinalCategory(for: value, locale: locale), expected)
        }
    }

    func testProductCountDescriptorUsesLocaleCardinalRules() {
        XCTAssertEqual(
            UiMessages.string(
                UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 0],
                    selectedCase: "post"
                ),
                locale: .french
            ),
            "0 publication"
        )
        XCTAssertEqual(
            UiMessages.string(
                UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 2],
                    selectedCase: "post"
                ),
                locale: .portuguese
            ),
            "2 publicações"
        )
    }

    func testPluralCategoriesResolveToGeneratedOneOrOtherResources() {
        XCTAssertEqual(
            UiMessages.string(
                UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 1_000_000],
                    selectedCase: "post"
                ),
                locale: .french
            ),
            "1 000 000 publications"
        )
    }

    func testDescriptorResourceKeyRequiresValueParameter() {
        XCTAssertThrowsError(
            try UiMessages.resourceKey(
                for: UiMessage(.sharedCountLabelFormat, selectedCase: "post"),
                locale: .english
            )
        ) { error in
            XCTAssertEqual(
                error as? UiMessageResourceKeyError,
                .missingValueParameter(.sharedCountLabelFormat, parameter: "count")
            )
        }
    }

    func testDescriptorResourceKeyRequiresSelectCase() {
        XCTAssertThrowsError(
            try UiMessages.resourceKey(
                for: UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 2]
                ),
                locale: .english
            )
        ) { error in
            XCTAssertEqual(
                error as? UiMessageResourceKeyError,
                .missingSelectCase(.sharedCountLabelFormat, parameter: "unit")
            )
        }
    }

    func testDescriptorResourceKeyRejectsInvalidSelectCase() {
        XCTAssertThrowsError(
            try UiMessages.resourceKey(
                for: UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 2],
                    selectedCase: "not-a-generated-case"
                ),
                locale: .english
            )
        ) { error in
            XCTAssertEqual(
                error as? UiMessageResourceKeyError,
                .invalidSelectCase(
                    .sharedCountLabelFormat,
                    parameter: "unit",
                    value: "not-a-generated-case"
                )
            )
        }
    }

    func testDescriptorResourceKeyResolvesValidSelectCase() throws {
        XCTAssertEqual(
            try UiMessages.resourceKey(
                for: UiMessage(
                    .sharedCountLabelFormat,
                    numberParameters: ["count": 1],
                    selectedCase: "post"
                ),
                locale: .english
            ),
            "shared.countLabel.format.__select.post.one"
        )
    }

    func testRepresentativeResourceResolvesForEveryLocale() {
        let expected: [UiLocale: String] = [
            .english: "Cancel",
            .spanish: "Cancelar",
            .french: "Annuler",
            .portuguese: "Cancelar"
        ]

        for locale in UiLocale.allCases {
            XCTAssertEqual(UiMessages.string(.commonCancel, locale: locale), expected[locale])
        }
    }

    func testControllerDoesNotNotifyObserversWhenResolvedLocaleIsUnchanged() {
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: ["fr"])
        let notification = expectation(description: "locale observation")
        notification.isInverted = true
        withObservationTracking {
            _ = controller.locale
        } onChange: {
            notification.fulfill()
        }

        controller.update(savedUiLocale: "en-US")
        controller.updatePreferredLanguages(["de-DE"])

        wait(for: [notification], timeout: 0.01)
        XCTAssertEqual(controller.locale, .english)
    }

    func testTokenReplacementIsSinglePassAndIndependentOfDictionaryOrder() {
        let message = UiMessage(
            .nativeSwiftEngineeringPostgresqlCompletedSummary,
            parameters: [
                "errors": "0",
                "skipped": "0",
                "updated": "7",
                "created": "{updated}"
            ]
        )

        XCTAssertEqual(
            UiMessages.string(message, locale: .english),
            "Completed. {updated} created, 7 updated, 0 skipped, 0 errors"
        )
    }

    func testDateFormattingUsesSavedLocaleAndExplicitTimeZone() throws {
        let date = Date(timeIntervalSince1970: 0)
        let utc = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let pacific = try XCTUnwrap(TimeZone(identifier: "America/Los_Angeles"))

        XCTAssertEqual(
            UiMessages.date(date, date: .numeric, time: .omitted, locale: .english, timeZone: utc),
            "1/1/1970"
        )
        XCTAssertEqual(
            UiMessages.date(date, date: .numeric, time: .omitted, locale: .english, timeZone: pacific),
            "12/31/1969"
        )
    }

    func testControllerFormatsNumbersWithUiLocale() {
        let controller = UiLocaleController(savedUiLocale: "fr", preferredLanguages: [])
        XCTAssertEqual(controller.number(1_234), "1\u{202F}234")
    }

    func testControllerRefreshesFromPreferredLanguagesWhenNoSavedLocaleExists() {
        let controller = UiLocaleController(savedUiLocale: nil, preferredLanguages: ["en-US"])

        controller.updatePreferredLanguages(["pt-BR"])

        XCTAssertEqual(controller.locale, .portuguese)
        XCTAssertEqual(controller.string(.commonCancel), "Cancelar")
    }

    func testControllerDelegatesNumberPercentCurrencyAndDateFormatting() throws {
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])
        let utc = try XCTUnwrap(TimeZone(secondsFromGMT: 0))

        XCTAssertEqual(controller.number(1_234.567, maximumFractionDigits: 1), "1,234.6")
        XCTAssertEqual(controller.percent(0.125), "12%")
        XCTAssertEqual(
            try controller.currency(XCTUnwrap(Decimal(string: "12.5")), code: "USD"),
            "$12.50"
        )
        XCTAssertEqual(
            controller.date(
                Date(timeIntervalSince1970: 0),
                date: .numeric,
                time: .omitted,
                timeZone: utc
            ),
            "1/1/1970"
        )
    }

    func testPercentFormattingDiffersBetweenEnglishAndFrench() {
        XCTAssertEqual(UiMessages.percent(0.125, maximumFractionDigits: 1, locale: .english), "12.5%")
        XCTAssertEqual(UiMessages.percent(0.125, maximumFractionDigits: 1, locale: .french), "12,5 %")
    }

    func testInt64FormattingAndPluralizationRemainExactBeyondDoublePrecision() {
        let value = Int64.max

        XCTAssertEqual(UiMessages.number(value, locale: .english), "9,223,372,036,854,775,807")
        XCTAssertEqual(
            UiMessages.plural(
                .extractedAiCostsPageUnpricedRequestCount,
                value: value,
                locale: Locale(identifier: "en")
            ),
            "9,223,372,036,854,775,807 unpriced requests"
        )
    }

    func testCurrencyFormattingDiffersBetweenEnglishAndFrench() throws {
        XCTAssertEqual(
            try UiMessages.currency(XCTUnwrap(Decimal(string: "1234.5")), code: "USD", locale: .english),
            "$1,234.50"
        )
        XCTAssertEqual(
            try UiMessages.currency(XCTUnwrap(Decimal(string: "1234.5")), code: "USD", locale: .french),
            "1 234,50 $US"
        )
    }

    func testPlaybackDurationUsesLocalizedDigitsAndCatalogTemplate() {
        let minutes = UiMessages.number(12, locale: .english)
        let seconds = UiMessages.integer(3, minimumIntegerDigits: 2, locale: .english)

        XCTAssertEqual(
            UiMessages.string(
                .nativeSwiftPodcastPlaybackDuration,
                parameters: ["minutes": minutes, "seconds": seconds],
                locale: .english
            ),
            "12:03"
        )
    }

    func testVerbatimTextPreservesServerOwnedContent() {
        let controller = UiLocaleController(savedUiLocale: "fr", preferredLanguages: [])

        XCTAssertEqual(controller.string(.verbatim("@raw-user")), "@raw-user")
        XCTAssertEqual(controller.string(.app(UiMessage(.navSources))), "Sources")
    }

    func testTokenReplacementPreservesUnknownAndEmptyTokens() {
        XCTAssertEqual(
            UiMessages.replacingTokens(
                in: "Known {name}; unknown {missing}; empty {}; open {",
                parameters: ["name": "Alice", "": "ignored"]
            ),
            "Known Alice; unknown {missing}; empty {}; open {"
        )
    }
}
