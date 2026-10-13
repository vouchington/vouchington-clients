import Foundation

extension MemberMCPClient {
    static func retryDelay(_ value: String?) -> Int? {
        guard let value else { return nil }
        if let seconds = Int(value), seconds > 0 { return seconds }
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE, dd MMM yyyy HH:mm:ss 'GMT'"
        guard let date = formatter.date(from: value) else { return nil }
        let seconds = date.timeIntervalSinceNow
        guard seconds > 0, seconds < Double(Int.max) else { return nil }
        return Int(ceil(seconds))
    }
}
