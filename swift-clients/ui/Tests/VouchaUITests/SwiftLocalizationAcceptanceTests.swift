import Foundation
@testable import VouchaFeatures
@testable import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class SwiftLocalizationAcceptanceTests: XCTestCase {
    func testSupportedLocaleAcceptanceMatrixCoversRepresentativeSurfaces() {
        let cases: [
            (
                locale: UiLocale,
                settings: String,
                navigation: String,
                collection: String,
                detail: String,
                validation: String,
                emptyError: String
            )
        ] = [
            (
                .english,
                "Settings",
                "Communities",
                "Lists",
                "Community workspace",
                "Email is required.",
                "Unable to load"
            ),
            (
                .spanish,
                "Ajustes",
                "Comunidades",
                "Listas",
                "Espacio de trabajo de la comunidad",
                "El correo electrónico es obligatorio.",
                "No se pudo cargar"
            ),
            (
                .french,
                "Réglages",
                "Communautés",
                "Listes",
                "Espace de travail de la communauté",
                "L’adresse e-mail est obligatoire.",
                "Impossible de charger"
            ),
            (
                .portuguese,
                "Definições",
                "Comunidades",
                "Listas",
                "Espaço de trabalho da comunidade",
                "O e-mail é obrigatório.",
                "Não foi possível carregar"
            )
        ]

        for item in cases {
            XCTAssertEqual(
                UiMessages.string(.nativeSwiftNavigationTitlesSettings, locale: item.locale),
                item.settings
            )
            XCTAssertEqual(
                UiMessages.string(.nativeSwiftNavigationTitlesCommunities, locale: item.locale),
                item.navigation
            )
            XCTAssertEqual(UiMessages.string(.nativeSwiftListsLists, locale: item.locale), item.collection)
            XCTAssertEqual(
                UiMessages.string(.nativeSwiftCommunitiesCommunityWorkspace, locale: item.locale),
                item.detail
            )
            XCTAssertEqual(
                UiMessages.string(.nativeSwiftValidationEmailRequired, locale: item.locale),
                item.validation
            )
            XCTAssertEqual(
                UiMessages.string(.nativeSwiftEmptyStateUnableToLoad, locale: item.locale),
                item.emptyError
            )
        }
    }

    func testSettingsStatusMessageRefreshesAfterLocaleChange() throws {
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: ["en-US"])
        let viewModel = SettingsViewModel(client: nil, uiLocaleController: controller)
        viewModel.statusMessage = .message(.nativeSwiftSettingsIdentitySaved)

        XCTAssertEqual(try controller.string(XCTUnwrap(viewModel.statusMessage)), "Identity saved")

        controller.update(savedUiLocale: "fr")

        XCTAssertEqual(try controller.string(XCTUnwrap(viewModel.statusMessage)), "Identité enregistrée")
    }

    func testPresentationEnumsUseTypedLocalizedKeys() {
        XCTAssertEqual(UiMessages.string(ListsItemFilter.reading.titleKey, locale: .spanish), "Lectura")
        XCTAssertEqual(UiMessages.string(CommunityMemberRole.owner.titleKey, locale: .french), "Propriétaire")
        XCTAssertEqual(UiMessages.string(ProfileLinkType.twitter.titleKey, locale: .portuguese), "Twitter")
        XCTAssertEqual(UiMessages.string(UserPrivacyAudience.nobody.titleKey, locale: .spanish), "Nadie")
        XCTAssertEqual(UiMessages.string(PostType.dataPoint.titleKey, locale: .french), "Point de données")
        XCTAssertEqual(
            UiMessages.string(DisplayNameSource.username.titleKey, locale: .portuguese),
            "Nome de utilizador"
        )
        XCTAssertEqual(
            UiMessages.string(supportThreadStatusText(.resolved), locale: .spanish),
            "Resuelto"
        )
    }

    func testToolResultCountsUseLocalizedPluralRules() {
        let result = NativeChatToolResult(
            toolCallId: "tool-1",
            result: .array([.string("a"), .string("b")])
        )

        XCTAssertEqual(UiMessages.string(result.displayText, locale: .english), "2 items")
        XCTAssertEqual(UiMessages.string(result.displayText, locale: .spanish), "2 elementos")
        XCTAssertEqual(UiMessages.string(result.displayText, locale: .french), "2 éléments")
        XCTAssertEqual(UiMessages.string(result.displayText, locale: .portuguese), "2 itens")
    }
}
