import SwiftUI
import ViewInspector
import VouchaLocalization
import XCTest

@MainActor
final class IntegrityLocaleRenderTests: XCTestCase {
    func testReportIntegrityTitleRefreshesForEverySupportedNonEnglishLocale() async throws {
        let controller = UiLocaleController(savedUiLocale: "es", preferredLanguages: [])
        let surface = IntegrityLocaleTitleHarness(controller: controller)

        try await ViewHosting.host(surface) {
            XCTAssertNoThrow(try surface.inspect().find(text: "Indicadores de integridad de denuncias"))

            controller.update(savedUiLocale: "fr")
            await Task.yield()
            XCTAssertNoThrow(try surface.inspect().find(text: "Signalements d’intégrité des rapports"))

            controller.update(savedUiLocale: "pt")
            await Task.yield()
            XCTAssertNoThrow(try surface.inspect().find(text: "Sinalizações de integridade de denúncias"))
        }
    }
}

private struct IntegrityLocaleTitleHarness: View {
    let controller: UiLocaleController

    var body: some View {
        Text(controller.string(.nativeSwiftIntegrityReportFlagsTitle))
    }
}
