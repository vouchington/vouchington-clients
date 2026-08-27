import Foundation

public enum MoneyValidationError: Error, Equatable {
    case invalidAmount
    case invalidCurrency
    case invalidMinorUnitExponent
    case invalidScale
    case invalidRange
}

public struct Currency: Codable, Equatable, Hashable, Identifiable, Sendable {
    public static let supported: [Currency] = SupportedCurrency.allCases.map(Currency.init)
    public let code: String
    public let minorUnitExponent: Int
    public var id: String {
        code
    }

    public init(code: String, minorUnitExponent: Int) throws {
        guard Self.isValidCode(code) else { throw MoneyValidationError.invalidCurrency }
        guard (0 ... 4).contains(minorUnitExponent) else {
            throw MoneyValidationError.invalidMinorUnitExponent
        }
        self.code = code
        self.minorUnitExponent = minorUnitExponent
    }

    private init(_ supportedCurrency: SupportedCurrency) {
        code = supportedCurrency.rawValue
        minorUnitExponent = supportedCurrency.minorUnitExponent
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        try self.init(
            code: container.decode(String.self, forKey: .code),
            minorUnitExponent: container.decode(Int.self, forKey: .minorUnitExponent)
        )
    }

    static func isValidCode(_ code: String) -> Bool {
        code.utf8.count == 3 && code.utf8.allSatisfy { (97 ... 122).contains($0) }
    }

    public static func minorUnitExponent(for code: String) -> Int? {
        supported.first { $0.code == code }?.minorUnitExponent
    }
}

private enum SupportedCurrency: String, CaseIterable {
    case aud
    case cad
    case eur
    case gbp
    case jpy
    case usd

    var minorUnitExponent: Int {
        switch self {
        case .jpy:
            0
        case .aud, .cad, .eur, .gbp, .usd:
            2
        }
    }
}

public struct Money: Codable, Equatable, Hashable, Sendable {
    public static let maximumAmount: Int64 = 9_007_199_254_740_991
    public let amount: Int64
    public let currency: String

    public init(amount: Int64, currency: String) throws {
        guard (0 ... Self.maximumAmount).contains(amount) else {
            throw MoneyValidationError.invalidAmount
        }
        guard Currency.isValidCode(currency) else { throw MoneyValidationError.invalidCurrency }
        self.amount = amount
        self.currency = currency
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        try self.init(
            amount: container.decode(Int64.self, forKey: .amount),
            currency: container.decode(String.self, forKey: .currency)
        )
    }

    public func majorUnitDecimal(minorUnitExponent: Int) -> Decimal {
        Decimal(amount) / pow(10, minorUnitExponent)
    }

    public var knownCurrencyMajorUnitDecimal: Decimal? {
        Currency.minorUnitExponent(for: currency).map(majorUnitDecimal)
    }
}

public struct ScaledMoney: Codable, Equatable, Hashable, Sendable {
    public static let scale = 6
    public let amount: Int64
    public let currency: String
    public let scale: Int

    public init(amount: Int64, currency: String, scale: Int = Self.scale) throws {
        guard (0 ... Money.maximumAmount).contains(amount) else {
            throw MoneyValidationError.invalidAmount
        }
        guard Currency.isValidCode(currency) else { throw MoneyValidationError.invalidCurrency }
        guard scale == Self.scale else { throw MoneyValidationError.invalidScale }
        self.amount = amount
        self.currency = currency
        self.scale = scale
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        try self.init(
            amount: container.decode(Int64.self, forKey: .amount),
            currency: container.decode(String.self, forKey: .currency),
            scale: container.decode(Int.self, forKey: .scale)
        )
    }

    public var majorUnitDecimal: Decimal {
        Decimal(amount) / pow(10, Self.scale)
    }
}

public struct ScaledMoneyAggregate: Codable, Equatable, Hashable, Sendable {
    public static let scale = 6
    public let amount: String
    public let currency: String
    public let scale: Int

    public init(amount: String, currency: String, scale: Int = Self.scale) throws {
        guard !amount.isEmpty, amount == "0" || (
            amount.first != "0" && amount.utf8.allSatisfy { (48 ... 57).contains($0) }
        ) else { throw MoneyValidationError.invalidAmount }
        guard Currency.isValidCode(currency) else { throw MoneyValidationError.invalidCurrency }
        guard scale == Self.scale else { throw MoneyValidationError.invalidScale }
        self.amount = amount
        self.currency = currency
        self.scale = scale
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        try self.init(
            amount: container.decode(String.self, forKey: .amount),
            currency: container.decode(String.self, forKey: .currency),
            scale: container.decode(Int.self, forKey: .scale)
        )
    }

    public var majorUnitString: String {
        let digits = String(repeating: "0", count: max(0, Self.scale + 1 - amount.count)) + amount
        let split = digits.index(digits.endIndex, offsetBy: -Self.scale)
        let whole = digits[..<split]
        let fraction = digits[split...].reversed().drop(while: { $0 == "0" }).reversed()
        return fraction.isEmpty ? String(whole) : "\(whole).\(String(fraction))"
    }
}

public struct MoneyRange: Codable, Equatable, Sendable {
    public let minimum: Money
    public let maximum: Money?

    public init(minimum: Money, maximum: Money?) throws {
        if let maximum {
            guard maximum.currency == minimum.currency,
                  maximum.amount > minimum.amount
            else { throw MoneyValidationError.invalidRange }
        }
        self.minimum = minimum
        self.maximum = maximum
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        try self.init(
            minimum: container.decode(Money.self, forKey: .minimum),
            maximum: container.decodeIfPresent(Money.self, forKey: .maximum)
        )
    }
}

private func pow(_ base: Decimal, _ exponent: Int) -> Decimal {
    (0 ..< exponent).reduce(1) { value, _ in value * base }
}
