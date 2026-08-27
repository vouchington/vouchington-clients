import Foundation

#if canImport(Security)
    enum KeychainCookieExpiration {
        static let createdPropertyKey = HTTPCookiePropertyKey("Created")

        static func propertiesByStampingCreatedDateIfNeeded(
            _ properties: [HTTPCookiePropertyKey: Any]?,
            now: Date
        ) -> [HTTPCookiePropertyKey: Any]? {
            guard var properties,
                  properties[.maximumAge] != nil,
                  properties[createdPropertyKey] == nil
            else { return nil }
            properties[createdPropertyKey] = now.timeIntervalSinceReferenceDate
            return properties
        }

        static func propertiesByAddingAbsoluteExpiryIfNeeded(
            _ properties: [HTTPCookiePropertyKey: Any]
        ) -> [HTTPCookiePropertyKey: Any] {
            guard let maxAge = timeInterval(fromCookiePropertyValue: properties[.maximumAge]),
                  maxAge > 0,
                  let createdDate = date(fromCookiePropertyValue: properties[createdPropertyKey])
            else { return properties }
            var properties = properties
            properties[.expires] = createdDate.addingTimeInterval(maxAge)
            properties[.maximumAge] = nil
            return properties
        }

        static func expiresDate(from properties: [HTTPCookiePropertyKey: Any]) -> Date? {
            date(fromCookiePropertyValue: properties[.expires])
        }

        static func isExpired(
            expiresDate: Date?,
            properties: [HTTPCookiePropertyKey: Any]?,
            now: Date
        ) -> Bool {
            if let maxAge = timeInterval(fromCookiePropertyValue: properties?[.maximumAge]) {
                guard maxAge > 0 else { return true }
                guard let createdDate = date(fromCookiePropertyValue: properties?[createdPropertyKey]) else {
                    return true
                }
                return createdDate.addingTimeInterval(maxAge) <= now
            }
            guard let expiresDate else { return false }
            return expiresDate <= now
        }

        private static func timeInterval(fromCookiePropertyValue value: Any?) -> TimeInterval? {
            switch value {
            case let interval as TimeInterval:
                interval
            case let number as NSNumber:
                number.doubleValue
            case let string as String:
                TimeInterval(string)
            default:
                nil
            }
        }

        private static func date(fromCookiePropertyValue value: Any?) -> Date? {
            switch value {
            case let date as Date:
                date
            case let interval as TimeInterval:
                Date(timeIntervalSinceReferenceDate: interval)
            case let number as NSNumber:
                Date(timeIntervalSinceReferenceDate: number.doubleValue)
            case let string as String:
                if let interval = TimeInterval(string) {
                    Date(timeIntervalSinceReferenceDate: interval)
                } else {
                    nil
                }
            default:
                nil
            }
        }
    }
#endif
