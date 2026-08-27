struct NativeMyLandingPageResponse: Decodable {
    let landingPage: NativeMyLandingPage
}

struct NativeMyLandingPage: Decodable {
    let title: String
    let slug: String
    let items: [NativeListItem]
}
