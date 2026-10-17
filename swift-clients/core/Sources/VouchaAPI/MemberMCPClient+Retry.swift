import Foundation
#if !canImport(Darwin)
    import FoundationNetworking
#endif
import VouchaModels

extension MemberMCPClient {
    static func retryDelay(_ value: String?, now: Date = Date()) -> Int? {
        guard let value else { return nil }
        if let seconds = Int(value), seconds > 0 { return seconds }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE, dd MMM yyyy HH:mm:ss 'GMT'"
        guard let date = formatter.date(from: value) else { return nil }
        let seconds = date.timeIntervalSince(now)
        guard seconds > 0, seconds < Double(Int.max) else { return nil }
        return Int(ceil(seconds))
    }

    func rateLimitFailure(_ response: HTTPURLResponse) -> Failure {
        retryCooldown.limited(Self.retryDelay(
            response.value(forHTTPHeaderField: "Retry-After"), now: retryCooldown.date()
        ))
    }

    func rateLimitFailure(_ result: ToolResult) -> Failure? {
        if result.isError == true {
            for content in result.content {
                guard case let .object(block) = content,
                      case let .string(text)? = block["text"],
                      let data = text.data(using: .utf8),
                      let value = try? JSONDecoder().decode(DecodedJSONValue.self, from: data),
                      case let .object(body) = value,
                      case let .object(error)? = body["error"],
                      case let .string(code)? = error["code"], code == "RATE_LIMIT"
                else { continue }
                let seconds: Int? = if case let .number(value)? = error["retryAfterSeconds"],
                                       value > 0, value < Double(Int.max), value.rounded() == value {
                    Int(value)
                } else { nil }
                return retryCooldown.limited(seconds)
            }
        }
        return nil
    }

}
