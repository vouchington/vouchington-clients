@testable import VouchaModels
import XCTest

final class FediverseModelDecodingTests: XCTestCase {
    func testDirectoryDecodesClassifiedAndUnclassifiedInstances() throws {
        let response = try makeVouchaDecoder().decode(
            FediverseInstancesResponse.self,
            from: ApiFixtureLoader.data("native.fediverse.instances.default")
        )
        XCTAssertEqual(response.orderedInstances.map(\.topic.name), ["social.example", "unknown.example"])
        XCTAssertEqual(response.orderedInstances.first?.instance.software, "mastodon")
        XCTAssertNil(response.orderedInstances.last?.instance.software)
        XCTAssertTrue(response.pageInfo.hasNextPage)
    }

    func testDetailDecodesMetadataAndTrust() throws {
        let response = try makeVouchaDecoder().decode(
            FediverseInstanceDetailResponse.self,
            from: ApiFixtureLoader.data("native.fediverse.instance.slug")
        )
        XCTAssertEqual(response.topic.slug, "social-example")
        XCTAssertEqual(response.instance?.monthlyActiveUsers, 340)
        XCTAssertEqual(response.hostnameElection?.votesScoreNet, 10)
    }

    func testDetailDecodesDocumentedNullInstance() throws {
        var object = try XCTUnwrap(
            JSONSerialization.jsonObject(
                with: ApiFixtureLoader.data("native.fediverse.instance.slug")
            ) as? [String: Any]
        )
        object["fediverse_instance"] = NSNull()

        let response = try makeVouchaDecoder().decode(
            FediverseInstanceDetailResponse.self,
            from: JSONSerialization.data(withJSONObject: object)
        )

        XCTAssertNil(response.instance)
    }

    func testPublicInstanceProjectionEncodesOnlyDedicatedFields() throws {
        let response = try makeVouchaDecoder().decode(
            FediverseInstanceDetailResponse.self,
            from: ApiFixtureLoader.data("native.fediverse.instance.slug")
        )
        let encoder = JSONEncoder()
        encoder.keyEncodingStrategy = .convertToSnakeCase
        let encoded = try encoder.encode(XCTUnwrap(response.instance))
        let object = try XCTUnwrap(JSONSerialization.jsonObject(with: encoded) as? [String: Any])

        XCTAssertEqual(Set(object.keys), [
            "monthly_active_users",
            "nodeinfo_software_version",
            "open_registrations",
            "protocol",
            "software",
            "total_users"
        ])
    }
}
