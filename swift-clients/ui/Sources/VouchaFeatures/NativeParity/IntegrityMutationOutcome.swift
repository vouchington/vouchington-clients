import Foundation
import VouchaCore

func integrityMutationIsAmbiguous(_ error: Error) -> Bool {
    if error is CancellationError || error is URLError {
        return true
    }
    guard let error = error as? VouchaError else { return false }
    switch error {
    case .network, .cancelled:
        return true
    case .decodingFailed:
        return true
    case let .api(statusCode, _), let .apiMessage(statusCode, _, _):
        return statusCode == 0 || statusCode == 408 || statusCode >= 500
    default:
        return false
    }
}
