import Foundation
@testable import VouchaAPI
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

    private func range(_ from: String, _ to: String, budget: Int? = nil) -> EngineeringClassifierRange {
        .init(fromDate: from, toDate: to, maxDefaultRows: budget)
    }

    private func json(_ endpoint: Endpoint) throws -> NSDictionary {
        let body = try XCTUnwrap(endpoint.body)
        let data = try JSONEncoder().encode(body)
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? NSDictionary)
    }
}
