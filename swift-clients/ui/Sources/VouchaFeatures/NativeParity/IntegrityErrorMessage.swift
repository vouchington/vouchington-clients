import VouchaCore

func integrityErrorMessage(_ error: Error) -> String {
    if let error = error as? VouchaError {
        return error.errorDescription ?? "An error occurred."
    }
    return error.localizedDescription
}
