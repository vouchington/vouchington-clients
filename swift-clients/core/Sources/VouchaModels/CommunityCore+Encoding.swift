import Foundation

extension Community {
    public func encode(to encoder: any Encoder) throws {
        var raw = encoder.singleValueContainer()
        var object: [String: DecodedJSONValue] = [:]
        if let entityType {
            object["__entity_type"] = .string(entityType)
        }
        object["id"] = .string(id)
        object["name"] = .string(name)
        object["slug"] = .string(slug)
        object["markdown"] = markdown.map(DecodedJSONValue.string) ?? .null
        object["visibility"] = .string(visibility.rawValue)
        object["member_roster_visibility"] = .string(memberRosterVisibility.rawValue)
        object["list_type"] = listType.map { .string($0.rawValue) } ?? .null
        object["member_invites_allowed_at"] = memberInvitesAllowedAt.map(Self.dateValue) ?? .null
        object["post_approval_required_at"] = postApprovalRequiredAt.map(Self.dateValue) ?? .null
        object["automod_action"] = .string(automodAction.rawValue)
        object["should_allow_review_posts"] = .bool(shouldAllowReviewPosts)
        object["should_allow_data_point_posts"] = .bool(shouldAllowDataPointPosts)
        object["trusted_at"] = trustedAt.map(Self.dateValue) ?? .null
        object["profile_image_id"] = profileImageId.map(DecodedJSONValue.string) ?? .null
        object["banner_image_id"] = bannerImageId.map(DecodedJSONValue.string) ?? .null
        Self.encodeImagePlacements(
            into: &object,
            profile: profileImagePlacement,
            banner: bannerImagePlacement,
            encodeProfile: encodesProfileImagePlacement,
            encodeBanner: encodesBannerImagePlacement
        )
        object["created_by_id"] = .string(createdById)
        object["created_at"] = Self.dateValue(createdAt)
        object["updated_at"] = Self.dateValue(updatedAt)
        object["deleted_at"] = deletedAt.map(Self.dateValue) ?? .null
        object["deleted_by_id"] = deletedById.map(DecodedJSONValue.string) ?? .null
        object["archived_at"] = archivedAt.map(Self.dateValue) ?? .null
        object["archived_by_id"] = archivedById.map(DecodedJSONValue.string) ?? .null
        object["default_language"] = defaultLanguage.map(DecodedJSONValue.string) ?? .null
        object["lingua_rs_detected_language"] = linguaRsDetectedLanguage.map(DecodedJSONValue.string) ?? .null
        object["rules_markdown"] = rulesMarkdown.map(DecodedJSONValue.string) ?? .null
        if let owner {
            object["owner"] = owner
        }
        Self.encodeProvenance(provenance, into: &object)
        if let staffProvenance { object["staff_provenance"] = staffProvenance.rawValue }
        try raw.encode(object)
    }

    private static func encodeProvenance(
        _ provenance: PublicContentProvenance?,
        into object: inout [String: DecodedJSONValue]
    ) {
        guard let provenance else { return }
        var facts: [String: DecodedJSONValue] = ["via": .string(provenance.via), "app": .null]
        if let app = provenance.app {
            var appFacts: [String: DecodedJSONValue] = ["kind": .string(app.kind)]
            if let key = app.key { appFacts["key"] = .string(key) }
            if let hostname = app.hostname { appFacts["hostname"] = .string(hostname) }
            if let clientId = app.clientId { appFacts["client_id"] = .string(clientId) }
            if let clientName = app.clientName { appFacts["client_name"] = .string(clientName) }
            facts["app"] = .object(appFacts)
        }
        object["provenance"] = .object(facts)
    }

    private static func encodeImagePlacements(
        into object: inout [String: DecodedJSONValue],
        profile: ImagePlacement?,
        banner: ImagePlacement?,
        encodeProfile: Bool,
        encodeBanner: Bool
    ) {
        if encodeProfile || profile != nil {
            object["profile_image_placement"] = profile?.encodedValue ?? .null
        }
        if encodeBanner || banner != nil {
            object["banner_image_placement"] = banner?.encodedValue ?? .null
        }
    }

    private static func dateValue(_ date: Date) -> DecodedJSONValue {
        .string(ISO8601DateFormatter().string(from: date))
    }
}
