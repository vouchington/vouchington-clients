import VouchaModels

private struct UpdateIdentityBody: Encodable {
    let username: String?
    let useDisplayNameFrom: String?
    let profileImageId: NullableValue<String>?
}

public extension Endpoint {
    static func imageUploadURL(contentType: String, contentLength: Int) -> Endpoint {
        requestImageUploadURL(contentType: contentType, contentLength: contentLength)
    }

    static func requestImageUploadURL(contentType: String, contentLength: Int) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/images/upload-url",
            body: ImageUploadRequest(contentType: contentType, contentLength: contentLength)
        )
    }

    static func completeImageUpload(id: String) -> Endpoint {
        completeImageUpload(imageId: id)
    }

    static func completeImageUpload(imageId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/images/\(pathSegment(imageId))/completions")
    }

    static func imageUploadState(id: String) -> Endpoint {
        imageUploadState(imageId: id)
    }

    static func imageUploadState(imageId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/images/\(pathSegment(imageId))/upload-state")
    }

    static func updateIdentity(
        username: String? = nil,
        useDisplayNameFrom: String? = nil,
        profileImageId: NullableValue<String>? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/my/identity",
            body: UpdateIdentityBody(
                username: username,
                useDisplayNameFrom: useDisplayNameFrom,
                profileImageId: profileImageId
            )
        )
    }

    static func imageUploadUrl(contentType: String, contentLength: Int) -> Endpoint {
        imageUploadURL(contentType: contentType, contentLength: contentLength)
    }
}
