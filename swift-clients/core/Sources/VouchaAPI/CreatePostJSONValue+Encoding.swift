import Foundation

func jsonValue(from value: Any) throws -> CreatePostJSONValue {
    switch value {
    case is NSNull:
        return .null
    case let string as String:
        return .string(string)
    case let number as NSNumber:
        if isBooleanJSONNumber(number) {
            return .bool(number.boolValue)
        }
        if number.doubleValue.rounded() == number.doubleValue {
            return .integer(number.intValue)
        }
        return .number(number.doubleValue)
    case let array as [Any]:
        return try .array(array.map(jsonValue(from:)))
    case let object as [String: Any]:
        return try .object(object.mapValues(jsonValue(from:)))
    default:
        throw NSError(
            domain: "CreatePostJSONValue",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: "Unsupported JSON value: \(value)"]
        )
    }
}

private func isBooleanJSONNumber(_ number: NSNumber) -> Bool {
    #if canImport(CoreFoundation)
        return CFGetTypeID(number) == CFBooleanGetTypeID()
    #else
        let type = String(cString: number.objCType)
        return type == "c" || type == "B"
    #endif
}
