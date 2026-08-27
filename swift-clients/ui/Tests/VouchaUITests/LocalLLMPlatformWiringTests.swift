import Foundation
import XCTest

final class LocalLLMPlatformWiringTests: XCTestCase {
    func testAppleAppsDeclareLocalNetworkAccessForEndpointProviders() throws {
        for platform in ["iOS", "macOS"] {
            let plist = try propertyList(platform: platform)
            let transportSecurity = try XCTUnwrap(plist["NSAppTransportSecurity"] as? [String: Any])

            XCTAssertEqual(transportSecurity["NSAllowsLocalNetworking"] as? Bool, true, platform)
            XCTAssertFalse(
                try XCTUnwrap(plist["NSLocalNetworkUsageDescription"] as? String).isEmpty,
                platform
            )
        }
    }

    func testAppleAppLocalNetworkDescriptionsAreLocalized() throws {
        let expected = [
            "en": "Voucha connects to language models on your local network.",
            "es": "Voucha se conecta a modelos de lenguaje en tu red local.",
            "fr": "Voucha se connecte à des modèles de langage sur votre réseau local.",
            "pt": "Voucha conecta-se a modelos de linguagem na sua rede local."
        ]
        for platform in ["iOS", "macOS"] {
            let project = try String(contentsOf: appsURL(platform).appendingPathComponent("project.yml"))
            XCTAssertTrue(project.contains("- path: Resources\n        buildPhase: resources"), platform)
            for (locale, value) in expected {
                let strings = try String(contentsOf: appsURL(platform)
                    .appendingPathComponent("Resources/\(locale).lproj/InfoPlist.strings"))
                XCTAssertTrue(strings.contains(value), "\(platform) \(locale)")
            }
        }
    }

    private func propertyList(platform: String) throws -> [String: Any] {
        let swiftClients = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
        let data = try Data(contentsOf: swiftClients
            .appendingPathComponent("apps")
            .appendingPathComponent(platform)
            .appendingPathComponent("Info.plist"))
        return try XCTUnwrap(
            PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
        )
    }

    private func appsURL(_ platform: String) -> URL {
        URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent().deletingLastPathComponent()
            .deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent("apps").appendingPathComponent(platform)
    }
}
