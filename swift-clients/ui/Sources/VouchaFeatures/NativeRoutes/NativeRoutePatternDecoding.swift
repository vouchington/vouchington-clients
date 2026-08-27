import Foundation

func decodedNativeRouteCapture(_ value: String) -> String {
    value.removingPercentEncoding ?? value
}

func decodedNativeRouteQueryComponent(_ value: String) -> String {
    let formValue = value.replacingOccurrences(of: "+", with: " ")
    return formValue.removingPercentEncoding ?? formValue
}
