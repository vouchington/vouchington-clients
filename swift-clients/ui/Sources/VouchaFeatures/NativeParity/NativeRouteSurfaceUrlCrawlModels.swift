import VouchaLocalization
import VouchaModels

struct NativeUrlCrawlListResponse: Decodable {
    let results: [NativeUrlCrawlSummary]
    let pageInfo: Page<NativeUrlCrawlSummary>.PageInfo
}

struct NativeUrlCrawlDetailResponse: Decodable {
    let crawl: NativeUrlCrawlSummary
    let ogImageSideload: String?
}

struct NativeUrlCrawlSummary: Decodable, Identifiable {
    let id: String
    let responseStatusCode: Int?
    let completedAt: String?
    let createdAt: String?
    let title: String?
    let markdown: String?
    let metaTags: [String: NativeJSONValue]?
    let lang: String?

    private enum CodingKeys: String, CodingKey {
        case id, responseStatusCode, completedAt, createdAt, title, markdown, metaTags
        case lang = "language"
    }

    var statusText: UiVerbatimText {
        if let code = responseStatusCode {
            return .message(
                .nativeSwiftRouteSurfaceHttpStatus,
                numberParameters: ["code": Double(code)]
            )
        }
        return .message(.nativeSwiftRouteSurfaceStatusUnavailable)
    }
}

enum NativeJSONValue: Decodable {
    case string(String)
    case number(Double)
    case bool(Bool)
    case object

    init(from decoder: any Decoder) throws {
        let container = try decoder.singleValueContainer()
        if let value = try? container.decode(String.self) {
            self = .string(value)
        } else if let value = try? container.decode(Double.self) {
            self = .number(value)
        } else if let value = try? container.decode(Bool.self) {
            self = .bool(value)
        } else {
            self = .object
        }
    }

    var displayText: UiVerbatimText {
        switch self {
        case let .string(value):
            .verbatim(value)
        case let .number(value):
            .message(
                .nativeSwiftRouteSurfaceNumberValue,
                numberParameters: ["value": value]
            )
        case let .bool(value):
            .message(
                value
                    ? .nativeSwiftRouteSurfaceTrue
                    : .nativeSwiftRouteSurfaceFalse
            )
        case .object:
            .message(.nativeSwiftRouteSurfaceObject)
        }
    }
}
