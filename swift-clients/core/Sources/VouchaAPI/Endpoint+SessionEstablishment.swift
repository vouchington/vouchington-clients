extension Endpoint {
    /// These responses may establish a new account while an older session renewal is in flight.
    var establishesSession: Bool {
        guard method == .POST else { return false }
        switch path {
        case "/api/v1/auth/email-address/login", "/api/v1/auth/native/email-address/login",
             "/api/v1/auth/mfa/totp/verification", "/api/v1/auth/passkeys/authentication/verify",
             "/api/v1/auth/oauth/apple/continue":
            return true
        default:
            let components = path.split(separator: "/").map(String.init)
            return components.count == 7
                && Array(components.prefix(5)) == ["api", "v1", "auth", "oauth", "authorizations"]
                && components.last == "complete"
        }
    }
}
