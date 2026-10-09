import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
@testable import VouchaAPI
import VouchaModels
import VouchaTestSupport
import XCTest

final class MemberMCPFinancialProfileTests: XCTestCase {
    private struct FixtureCatalog: Decodable {
        let version: Int
        let cases: [Fixture]
    }

    private struct Fixture: Decodable {
        let id: String
        let tool: String
        let arguments: [String: DecodedJSONValue]
        let structuredContent: DecodedJSONValue
    }

    func testCanonicalNullAndPopulatedResultsUseAuthorizedToolCall() async throws {
        let root = try FilamentsContractRoot.url(requiredPaths: ["api-fixtures/v1/mcp-results.json"])
        let url = try ApiFixtureLoader.fixtureURL("mcp-results.json", root: root)
        let catalog = try JSONDecoder().decode(FixtureCatalog.self, from: Data(contentsOf: url))
        XCTAssertEqual(catalog.version, 1)
        let financialCases = catalog.cases.filter { $0.tool == "get_my_financial_profile" }
        XCTAssertEqual(financialCases.count, 2)
        XCTAssertEqual(Set(financialCases.map(\.id)), Set([
            "native.mcp.get-my-financial-profile.null",
            "native.mcp.get-my-financial-profile.populated"
        ]))

        for fixture in financialCases {
            XCTAssertTrue(fixture.arguments.isEmpty)
            FinancialMCPURLProtocol.configure(content: fixture.structuredContent)
            let result = try await makeClient().getMyFinancialProfile()
            let call = FinancialMCPURLProtocol.snapshot()
            XCTAssertEqual(call.bearer, "Bearer known-access")
            XCTAssertEqual(call.method, "tools/call")
            XCTAssertEqual(call.name, fixture.tool)
            XCTAssertTrue(call.arguments.isEmpty)
            if fixture.id.hasSuffix(".null") {
                XCTAssertNil(result.financialProfile)
            } else {
                let profile = try XCTUnwrap(result.financialProfile)
                XCTAssertEqual(profile.creditScoreRange, "670-739")
                XCTAssertEqual(profile.statedIncomeRange?.minimum.amount, 7_500_000)
                XCTAssertEqual(profile.statedIncomeRange?.maximum?.amount, 10_000_000)
                XCTAssertEqual(profile.statedIncomeRange?.minimum.currency, "usd")
                XCTAssertEqual(profile.yearsOfCreditHistory, 7)
                XCTAssertNil(profile.totalCreditLimit)
                XCTAssertNil(profile.hardInquiries12m)

                var envelope = try XCTUnwrap(
                    JSONSerialization.jsonObject(with: JSONEncoder().encode(fixture.structuredContent))
                        as? [String: Any]
                )
                var fields = try XCTUnwrap(envelope["result"] as? [String: Any])
                var profileFields = try XCTUnwrap(fields["financial_profile"] as? [String: Any])
                XCTAssertEqual(profile.individualId, profileFields["individual_id"] as? String)
                XCTAssertEqual(profile.currency, profileFields["currency"] as? String)
                XCTAssertEqual(profile.updatedAt, profileFields["updated_at"] as? String)
                profileFields["years_of_credit_history"] = 7.5
                fields["financial_profile"] = profileFields
                envelope["result"] = fields
                let fractional = try JSONDecoder().decode(
                    MemberFinancialProfileResult.self,
                    from: JSONSerialization.data(withJSONObject: envelope)
                )
                XCTAssertEqual(fractional.financialProfile?.yearsOfCreditHistory, 7.5)

                var income = try XCTUnwrap(profileFields["stated_income_range"] as? [String: Any])
                income.removeValue(forKey: "maximum")
                profileFields["stated_income_range"] = income
                fields["financial_profile"] = profileFields
                envelope["result"] = fields
                XCTAssertThrowsError(try JSONDecoder().decode(
                    MemberFinancialProfileResult.self,
                    from: JSONSerialization.data(withJSONObject: envelope)
                )) { error in
                    guard case DecodingError.keyNotFound = error else {
                        return XCTFail("A missing required-nullable maximum must fail: \(error)")
                    }
                }
            }
        }
    }

    func testMissingRequiredNullableFieldAndToolErrorAreDistinctFromSuccessfulNull() async throws {
        FinancialMCPURLProtocol.configure(content: .object([
            "success": .bool(true), "result": .object([:])
        ]))
        do {
            _ = try await makeClient().getMyFinancialProfile()
            XCTFail("A missing financial_profile must fail decoding")
        } catch DecodingError.keyNotFound(_, _) {}

        FinancialMCPURLProtocol.configure(content: .object([
            "success": .bool(false), "result": .object(["financial_profile": .null])
        ]))
        do {
            _ = try await makeClient().getMyFinancialProfile()
            XCTFail("An unsuccessful structured result must not become a successful null profile")
        } catch MemberMCPFinancialProfileFailure.unsuccessfulResult {}

        FinancialMCPURLProtocol.configure(content: .object([
            "success": .bool(true), "result": .object(["financial_profile": .null])
        ]), isError: true)
        do {
            _ = try await makeClient().getMyFinancialProfile()
            XCTFail("An MCP tool error must not become a successful null profile")
        } catch MemberMCPFinancialProfileFailure.toolError {}

        FinancialMCPURLProtocol.configure(content: nil)
        do {
            _ = try await makeClient().getMyFinancialProfile()
            XCTFail("Missing structured content must not become a successful null profile")
        } catch MemberMCPFinancialProfileFailure.missingStructuredContent {}
    }

    private func makeClient() throws -> MemberMCPAuthorizedClient {
        let origin = try XCTUnwrap(URL(string: "https://example.test"))
        let metadata = try MemberMCPOAuthMetadata(
            issuerIdentifier: "https://example.test",
            authorizationEndpoint: XCTUnwrap(URL(string: "https://example.test/authorize")),
            tokenEndpoint: XCTUnwrap(URL(string: "https://example.test/token")),
            revocationEndpoint: XCTUnwrap(URL(string: "https://example.test/revoke"))
        )
        let tokens = try MemberMCPTokenManager(
            accountId: "member-1", store: FinancialMCPTokenStore(),
            tokenClient: MemberMCPOAuthTokenClient(
                metadata: metadata,
                clientId: origin.appendingPathComponent("api/v1/oauth/native-clients/macos"),
                resource: origin.appendingPathComponent("api/v1/mcp"),
                protocolClasses: [FinancialMCPURLProtocol.self]
            )
        )
        return try MemberMCPAuthorizedClient(
            client: MemberMCPClient(siteOrigin: origin, protocolClasses: [FinancialMCPURLProtocol.self]),
            tokens: tokens
        )
    }
}
