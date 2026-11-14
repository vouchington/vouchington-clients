import Foundation
@testable import VouchaAPI
import VouchaTestSupport
import XCTest

final class EngineeringPartitionRangeTests: XCTestCase {
    func testRangeAcceptsLeapDayAndOptionalBudget() {
        XCTAssertTrue(range("2020-02-29", "2020-03-01").isValid)
        XCTAssertTrue(range("1970-01-01", "9999-12-31", budget: 1).isValid)
        XCTAssertTrue(range("2020-02-01", "2020-02-02", budget: 5_000).isValid)
    }

    func testRangeRejectsInvalidCalendarDatesSyntaxOrderingAndBudget() {
        for date in [
            "1969-12-31",
            "2021-02-29",
            "2020-02-30",
            "2020-13-01",
            "2020-00-01",
            "2020-01-00",
            "2020-01-32",
            "2020-2-01",
            "2020-02-01T00:00:00Z",
            ""
        ] {
            XCTAssertFalse(range(date, "2022-01-01").isValid, date)
            XCTAssertFalse(range("1970-01-01", date).isValid, date)
        }
        XCTAssertFalse(range("2020-02-01", "2020-02-01").isValid)
        XCTAssertFalse(range("2020-02-02", "2020-02-01").isValid)
        XCTAssertFalse(range("2020-02-01", "2020-02-02", budget: 0).isValid)
        XCTAssertFalse(range("2020-02-01", "2020-02-02", budget: 5_001).isValid)
    }

    func testClassifierRequestUsesExactCamelCaseContract() throws {
        let endpoint = Endpoint.psqlJobs(
            type: .createPartitions,
            classifierRange: range("2020-02-01", "2020-02-02", budget: 100)
        )
        XCTAssertEqual(endpoint.method, .POST)
        XCTAssertEqual(endpoint.path, "/api/v1/psql/jobs")
        XCTAssertEqual(try json(endpoint), [
            "type": "createPartitions",
            "classifierRange": ["fromDate": "2020-02-01", "toDate": "2020-02-02", "maxDefaultRows": 100]
        ] as NSDictionary)
    }

    func testRequestRangeUsesExclusiveFamilyAndOmittedBudget() throws {
        for type in [EngineeringPsqlJobType.createPartitions, .cleanupPartitions] {
            let endpoint = Endpoint.psqlJobs(type: type, requestRange: range("2020-02-01", "2020-02-02"))
            XCTAssertEqual(try json(endpoint), [
                "type": type.rawValue,
                "requestRange": ["fromDate": "2020-02-01", "toDate": "2020-02-02"]
            ] as NSDictionary)
        }
    }

    func testMonthlyPartitionRequestsOmitBothRangeProperties() throws {
        for type in [EngineeringPsqlJobType.createPartitions, .cleanupPartitions] {
            XCTAssertEqual(try json(.psqlJobs(type: type)), ["type": type.rawValue] as NSDictionary)
        }
    }

    func testClassifierRequestAndAcknowledgementMatchLandedFixture() throws {
        let fixtureId = "web.admin.psql.jobs.classifier-range"
        let fixture = try XCTUnwrap(ApiFixtureLoader.swiftRouteFixtures.first { $0.id == fixtureId })
        let endpoint = Endpoint.psqlJobs(
            type: .createPartitions,
            classifierRange: range("2020-02-01", "2020-02-02", budget: 100)
        )
        XCTAssertEqual(endpoint.path, fixture.path)
        XCTAssertEqual(endpoint.method.rawValue, fixture.method)
        XCTAssertEqual(try requestBodyJSON(from: endpoint), fixture.requestBody)
        let acknowledgement = try makeVouchaDecoder().decode(
            EngineeringSuccessResponse.self, from: ApiFixtureLoader.data(fixtureId)
        )
        XCTAssertTrue(acknowledgement.success)
    }

    func testBothRangeFamiliesMatchLandedRequestSchema() throws {
        let data = try Data(contentsOf: ContractRoot.apiContractURL("request-contracts.json"))
        let contracts = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let components = try XCTUnwrap(contracts["components"] as? [String: Any])
        let request = try XCTUnwrap(components["PsqlJobRequest"] as? [String: Any])
        let properties = try XCTUnwrap(request["properties"] as? [String: Any])
        XCTAssertEqual(request["required"] as? [String], ["type"])
        let classifier = try XCTUnwrap(properties["classifierRange"] as? NSDictionary)
        let requestRange = try XCTUnwrap(properties["requestRange"] as? NSDictionary)
        XCTAssertEqual(classifier, requestRange)
        XCTAssertEqual(classifier["required"] as? [String], ["fromDate", "toDate"])
        let fields = try XCTUnwrap(classifier["properties"] as? [String: Any])
        let budget = try XCTUnwrap(fields["maxDefaultRows"] as? [String: Any])
        let minimum = try XCTUnwrap(budget["minimum"] as? Int)
        let maximum = try XCTUnwrap(budget["maximum"] as? Int)
        XCTAssertTrue(range("2020-02-01", "2020-02-02", budget: minimum).isValid)
        XCTAssertTrue(range("2020-02-01", "2020-02-02", budget: maximum).isValid)
        XCTAssertFalse(range("2020-02-01", "2020-02-02", budget: minimum - 1).isValid)
        XCTAssertFalse(range("2020-02-01", "2020-02-02", budget: maximum + 1).isValid)
        for type in [EngineeringPsqlJobType.createPartitions, .cleanupPartitions] {
            for endpoint in [
                Endpoint.psqlJobs(type: type, classifierRange: range("2020-02-01", "2020-02-02")),
                Endpoint.psqlJobs(type: type, requestRange: range("2020-02-01", "2020-02-02"))
            ] {
                let body = try json(endpoint)
                let family = body["classifierRange"] == nil ? "requestRange" : "classifierRange"
                let selected = try XCTUnwrap(body[family] as? NSDictionary)
                XCTAssertEqual(
                    Set(selected.allKeys.compactMap { $0 as? String }),
                    try Set(XCTUnwrap(classifier["required"] as? [String]))
                )
                XCTAssertEqual(body.count, 2)
                XCTAssertEqual(body["type"] as? String, type.rawValue)
            }
        }
    }

    private func range(_ from: String, _ to: String, budget: Int? = nil) -> EngineeringClassifierRange {
        .init(fromDate: from, toDate: to, maxDefaultRows: budget)
    }

    private func json(_ endpoint: Endpoint) throws -> NSDictionary {
        let body = try XCTUnwrap(endpoint.body)
        let data = try JSONEncoder().encode(body)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? NSDictionary)
    }
}
