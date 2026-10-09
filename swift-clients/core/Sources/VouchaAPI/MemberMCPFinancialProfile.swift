import Foundation
import VouchaModels

public struct MemberFinancialProfile: Decodable, Equatable, Sendable {
    public let individualId: String
    public let creditScoreRange: String?
    public let statedIncomeRange: MoneyRange?
    public let totalCreditLimit: Money?
    public let currency: String
    public let yearsOfCreditHistory: Double?
    public let hardInquiries12m: Double?
    public let cardsOpened24m: Double?
    public let updatedAt: String

    private enum CodingKeys: String, CodingKey {
        case individualId = "individual_id"
        case creditScoreRange = "credit_score_range"
        case statedIncomeRange = "stated_income_range"
        case totalCreditLimit = "total_credit_limit"
        case currency
        case yearsOfCreditHistory = "years_of_credit_history"
        case hardInquiries12m = "hard_inquiries_12m"
        case cardsOpened24m = "cards_opened_24m"
        case updatedAt = "updated_at"
    }

    private enum IncomeKeys: String, CodingKey { case maximum }

    public init(from decoder: any Decoder) throws {
        let fields = try decoder.container(keyedBy: CodingKeys.self)
        individualId = try fields.decode(String.self, forKey: .individualId)
        creditScoreRange = try fields.decodeRequiredNullable(String.self, forKey: .creditScoreRange)
        if fields.contains(.statedIncomeRange) {
            let isNull = try fields.decodeNil(forKey: .statedIncomeRange)
            if !isNull {
                let income = try fields.nestedContainer(keyedBy: IncomeKeys.self, forKey: .statedIncomeRange)
                guard income.contains(.maximum) else {
                    throw DecodingError.keyNotFound(
                        IncomeKeys.maximum,
                        .init(codingPath: income.codingPath, debugDescription: "Required nullable maximum is absent")
                    )
                }
            }
        }
        statedIncomeRange = try fields.decodeRequiredNullable(MoneyRange.self, forKey: .statedIncomeRange)
        totalCreditLimit = try fields.decodeRequiredNullable(Money.self, forKey: .totalCreditLimit)
        currency = try fields.decode(String.self, forKey: .currency)
        yearsOfCreditHistory = try fields.decodeRequiredNullable(Double.self, forKey: .yearsOfCreditHistory)
        hardInquiries12m = try fields.decodeRequiredNullable(Double.self, forKey: .hardInquiries12m)
        cardsOpened24m = try fields.decodeRequiredNullable(Double.self, forKey: .cardsOpened24m)
        updatedAt = try fields.decode(String.self, forKey: .updatedAt)
    }
}

public struct MemberFinancialProfileResult: Decodable, Equatable, Sendable {
    public let financialProfile: MemberFinancialProfile?

    private enum CodingKeys: String, CodingKey { case success, result }
    private enum ResultKeys: String, CodingKey { case financialProfile = "financial_profile" }

    public init(from decoder: any Decoder) throws {
        let fields = try decoder.container(keyedBy: CodingKeys.self)
        guard try fields.decode(Bool.self, forKey: .success) else {
            throw MemberMCPFinancialProfileFailure.unsuccessfulResult
        }
        let result = try fields.nestedContainer(keyedBy: ResultKeys.self, forKey: .result)
        financialProfile = try result.decodeRequiredNullable(
            MemberFinancialProfile.self, forKey: .financialProfile
        )
    }
}

public enum MemberMCPFinancialProfileFailure: Error, Equatable, Sendable {
    case toolError
    case missingStructuredContent
    case unsuccessfulResult
}

public extension MemberMCPAuthorizedClient {
    func getMyFinancialProfile() async throws -> MemberFinancialProfileResult {
        let response = try await callTool(name: "get_my_financial_profile", arguments: [:])
        guard response.isError != true else { throw MemberMCPFinancialProfileFailure.toolError }
        guard let structured = response.structuredContent else {
            throw MemberMCPFinancialProfileFailure.missingStructuredContent
        }
        let data = try JSONEncoder().encode(structured)
        return try JSONDecoder().decode(MemberFinancialProfileResult.self, from: data)
    }
}

private extension KeyedDecodingContainer {
    func decodeRequiredNullable<Value: Decodable>(_ type: Value.Type, forKey key: Key) throws -> Value? {
        guard contains(key) else {
            throw DecodingError.keyNotFound(
                key, .init(codingPath: codingPath, debugDescription: "Required nullable field is absent")
            )
        }
        return try decodeIfPresent(type, forKey: key)
    }
}
