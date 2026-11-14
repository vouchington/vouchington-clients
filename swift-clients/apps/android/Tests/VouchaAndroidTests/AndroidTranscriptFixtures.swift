import Foundation
import VouchaTestSupport

struct AndroidTranscriptFixtureManifest: Decodable {
    let fixtures: [AndroidTranscriptFixture]
}

struct AndroidTranscriptFixture: Decodable {
    let id: String
    let bodyFile: String
}

enum AndroidTranscriptFixtures {
    static func data(_ id: String) throws -> Data {
        let root = try ContractRoot.url(requiredPaths: ["api-fixtures/v1/manifest.json"])
        let manifestURL = try ContractRoot.fixtureURL("manifest.json", root: root)
        let manifest = try JSONDecoder().decode(
            AndroidTranscriptFixtureManifest.self,
            from: Data(contentsOf: manifestURL)
        )
        guard let fixture = manifest.fixtures.first(where: { $0.id == id }) else {
            throw FixtureError.missingFixture(id)
        }
        let bodyURL = try ContractRoot.fixtureURL(fixture.bodyFile, root: root)
        return try Data(contentsOf: bodyURL)
    }

    enum FixtureError: Error {
        case missingFixture(String)
    }
}
