import VouchaLocalization
import XCTest

final class LocalizationTests: XCTestCase {
    func testAndroidChatAndStatusMessagesResolveForEverySupportedLocale() {
        XCTAssertEqual(UiMessages.string(.nativeSwiftAndroidChat, locale: .english), "Chat")
        XCTAssertEqual(UiMessages.string(.nativeSwiftAndroidSend, locale: .spanish), "Enviar")
        XCTAssertEqual(
            UiMessages.string(.nativeSwiftAndroidModelAvailable, locale: .french),
            "Le modèle sur l’appareil est prêt."
        )
        XCTAssertEqual(
            UiMessages.string(.nativeSwiftAndroidEndpointSaved, locale: .portuguese),
            "O endpoint foi guardado neste dispositivo."
        )
    }

    func testAndroidErrorMessagesResolveForEverySupportedLocale() {
        XCTAssertEqual(
            UiMessages.string(.nativeSwiftAndroidAicoreUnavailable, locale: .spanish),
            "Android AICore no está disponible en esta compilación."
        )
        XCTAssertEqual(
            UiMessages.string(.nativeSwiftAndroidEmptyResponse, locale: .french),
            "Le modèle sur l’appareil a renvoyé une réponse vide."
        )
    }
}
