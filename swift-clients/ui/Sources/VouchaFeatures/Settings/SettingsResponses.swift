import VouchaModels

struct SettingsListResponse<T: Decodable & Sendable>: Decodable {
    let results: [T]
    let pageInfo: Page<T>.PageInfo
}

struct SettingsIdentityResponse: Decodable {
    let identity: PrivateUser
}

struct SettingsProfileResponse: Decodable {
    let profile: Profile
}

struct SettingsUserResponse: Decodable {
    let user: PrivateUser
}

struct SettingsProfileLinkResponse: Decodable {
    let profileLink: VouchaModels.ProfileLink
}

struct SettingsApiKeyResponse: Decodable {
    let apiKey: ApiKey
    let rawKey: String
}

struct DeleteAccountResponse: Decodable {
    let logout: Bool
}
