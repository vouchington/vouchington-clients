import VouchaModels

struct FriendsListUsersResponse: Decodable {
    struct PageInfo: Decodable {
        let hasNextPage: Bool
        let endCursor: String?
    }

    let results: [PublicUser]
    let pageInfo: PageInfo
}
