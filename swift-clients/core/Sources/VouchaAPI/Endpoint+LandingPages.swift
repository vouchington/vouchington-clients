public struct LandingPageMetadataBody: Encodable, Sendable {
    public let title: String?
    public let subtitle: NullableStringPatchField?
    public let slug: String?

    public init(title: String? = nil, subtitle: NullableStringPatchField? = nil, slug: String? = nil) {
        self.title = title
        self.subtitle = subtitle
        self.slug = slug
    }
}

private struct LandingPageDefaultBody: Encodable {
    let isDefault = true

    private enum CodingKeys: String, CodingKey {
        case isDefault
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(isDefault, forKey: .isDefault)
    }
}

private struct LandingPageItemsBody: Encodable {
    let items: [LandingPageItemInput]
}

public extension Endpoint {
    static func publicLandingPage(username: String, slug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/users/\(pathSegment(username))/landing-pages/\(pathSegment(slug))")
    }

    static func publicDefaultLandingPage(username: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/users/\(pathSegment(username))/landing-page")
    }

    static var myLandingPages: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/landing-pages")
    }

    static var myLandingPageCandidates: Endpoint {
        Endpoint(.GET, path: "/api/v1/my/landing-pages/candidates")
    }

    static func myLandingPage(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/my/landing-pages/\(pathSegment(id))")
    }

    static func myLandingPageAnalytics(pageId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/my/landing-pages/\(pathSegment(pageId))/analytics")
    }

    static func adminUserLandingPages(userId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/admin/users/\(pathSegment(userId))/landing-pages")
    }

    static func adminLandingPageAnalytics(pageId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/admin/landing-pages/\(pathSegment(pageId))/analytics")
    }

    static func createMyLandingPage(title: String, subtitle: NullableStringPatchField?, slug: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/my/landing-pages",
            body: LandingPageMetadataBody(title: title, subtitle: subtitle, slug: slug)
        )
    }

    static func updateMyLandingPage(id: String, body: LandingPageMetadataBody) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/landing-pages/\(pathSegment(id))", body: body)
    }

    static func setDefaultMyLandingPage(id: String) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/landing-pages/\(pathSegment(id))", body: LandingPageDefaultBody())
    }

    static func deleteMyLandingPage(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/landing-pages/\(pathSegment(id))")
    }

    static func replaceMyLandingPageItems(id: String, items: [LandingPageItemInput]) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/my/landing-pages/\(pathSegment(id))/items",
            body: LandingPageItemsBody(items: items)
        )
    }
}
