import Foundation

public enum UiMessageDateStyle: Equatable, Sendable {
    case omitted
    case numeric
    case abbreviated
    case long
    case monthYear

    var foundationStyle: Date.FormatStyle.DateStyle {
        switch self {
        case .omitted: .omitted
        case .numeric: .numeric
        case .abbreviated: .abbreviated
        case .long: .long
        case .monthYear: .omitted
        }
    }
}

public enum UiMessageTimeStyle: Equatable, Sendable {
    case omitted
    case shortened
    case standard

    var foundationStyle: Date.FormatStyle.TimeStyle {
        switch self {
        case .omitted: .omitted
        case .shortened: .shortened
        case .standard: .standard
        }
    }
}

public struct UiMessageDateParameter: Equatable, Sendable {
    public let value: Date
    public let dateStyle: UiMessageDateStyle
    public let timeStyle: UiMessageTimeStyle

    public init(
        _ value: Date,
        dateStyle: UiMessageDateStyle = .abbreviated,
        timeStyle: UiMessageTimeStyle = .omitted
    ) {
        self.value = value
        self.dateStyle = dateStyle
        self.timeStyle = timeStyle
    }
}

public struct UiMessageCurrencyParameter: Equatable, Sendable {
    public let value: Decimal
    public let code: String
    public let maximumFractionDigits: Int?

    public init(_ value: Decimal, code: String, maximumFractionDigits: Int? = nil) {
        self.value = value
        self.code = code
        self.maximumFractionDigits = maximumFractionDigits
    }
}

public struct UiMessage: Equatable, Sendable {
    public let key: UiMessageKey
    public let parameters: [String: String]
    public let textParameters: [String: UiVerbatimText]
    public let numberParameters: [String: Double]
    public let percentParameters: [String: Double]
    public let currencyParameters: [String: UiMessageCurrencyParameter]
    public let dateParameters: [String: UiMessageDateParameter]
    public let selectedCase: String?

    public init(
        _ key: UiMessageKey,
        parameters: [String: String] = [:],
        textParameters: [String: UiVerbatimText] = [:],
        numberParameters: [String: Double] = [:],
        percentParameters: [String: Double] = [:],
        currencyParameters: [String: UiMessageCurrencyParameter] = [:],
        dateParameters: [String: UiMessageDateParameter] = [:],
        selectedCase: String? = nil
    ) {
        self.key = key
        self.parameters = parameters
        self.textParameters = textParameters
        self.numberParameters = numberParameters
        self.percentParameters = percentParameters
        self.currencyParameters = currencyParameters
        self.dateParameters = dateParameters
        self.selectedCase = selectedCase
    }
}

public indirect enum UiVerbatimText: Equatable, Sendable {
    case app(UiMessage)
    case composition([UiVerbatimText], separator: String)
    case verbatim(String)

    public static func message(
        _ key: UiMessageKey,
        parameters: [String: String] = [:],
        textParameters: [String: UiVerbatimText] = [:],
        numberParameters: [String: Double] = [:],
        percentParameters: [String: Double] = [:],
        currencyParameters: [String: UiMessageCurrencyParameter] = [:],
        dateParameters: [String: UiMessageDateParameter] = [:],
        selectedCase: String? = nil
    ) -> Self {
        .app(UiMessage(
            key,
            parameters: parameters,
            textParameters: textParameters,
            numberParameters: numberParameters,
            percentParameters: percentParameters,
            currencyParameters: currencyParameters,
            dateParameters: dateParameters,
            selectedCase: selectedCase
        ))
    }

    public static func count(_ count: Int, item: String) -> Self {
        message(
            .sharedCountLabelFormat,
            numberParameters: ["count": Double(count)],
            selectedCase: item
        )
    }

    public static func joined(_ parts: [Self], separator: String = " · ") -> Self {
        .composition(parts, separator: separator)
    }

    public static func externalProvider(_ value: String) -> Self {
        .verbatim(value)
    }

    public static func protocolValue(_ value: String) -> Self {
        .verbatim(value)
    }

    public static func userContent(_ value: String) -> Self {
        .verbatim(value)
    }
}
