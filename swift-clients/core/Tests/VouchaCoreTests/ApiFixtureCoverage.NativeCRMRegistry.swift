import VouchaModels

let nativeCrmApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.crm.contacts.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<CrmContact>.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.archived_at",
                "results.assigned_to_id",
                "results.contacted_at",
                "results.converted_at",
                "results.metadata",
                "results.opted_out_at",
                "results.phone",
                "results.responded_at",
                "results.user_id"
            ]
        )
    },
    RegisteredFixture(id: "native.crm.contact-detail.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmContactDetailResponse.self,
            ignoring: crmContactNilFieldIgnores(prefix: "contact")
                .union(["contact.__entity_type", "social_accounts.__entity_type"])
        )
    },
    RegisteredFixture(id: "native.crm.contact-create.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmContactResponse.self,
            ignoring: crmContactNilFieldIgnores(prefix: "contact").union(["contact.__entity_type"])
        )
    },
    RegisteredFixture(id: "native.crm.contact-update.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmContactResponse.self,
            ignoring: crmContactNilFieldIgnores(prefix: "contact").union(["contact.__entity_type"])
        )
    },
    RegisteredFixture(id: "native.crm.contact-emails.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<CrmMessage>.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.body_html",
                "results.bounced_at",
                "results.delivered_at",
                "results.discarded_at",
                "results.received_at"
            ]
        )
    },
    RegisteredFixture(id: "native.crm.contact-email-send.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmMessageResponse.self,
            ignoring: [
                "message.body_html",
                "message.__entity_type",
                "message.bounced_at",
                "message.delivered_at",
                "message.discarded_at",
                "message.received_at"
            ]
        )
    },
    RegisteredFixture(id: "native.crm.contact-notes.default") {
        try assertFixtureCoversDTO(
            $0,
            as: Page<CrmNote>.self,
            ignoring: [
                "page_info.end_cursor",
                "page_info.start_cursor",
                "results.__entity_type",
                "results.deleted_at"
            ]
        )
    },
    RegisteredFixture(id: "native.crm.contact-note-create.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmNoteResponse.self,
            ignoring: ["note.__entity_type", "note.deleted_at"]
        )
    },
    RegisteredFixture(id: "native.crm.contact-link-user.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmContactResponse.self,
            ignoring: [
                "contact.archived_at",
                "contact.__entity_type",
                "contact.assigned_to_id",
                "contact.contacted_at",
                "contact.converted_at",
                "contact.metadata",
                "contact.opted_out_at",
                "contact.phone",
                "contact.responded_at"
            ]
        )
    },
    RegisteredFixture(id: "native.crm.contact-unlink-user.default") {
        try assertFixtureCoversDTO(
            $0,
            as: CrmContactResponse.self,
            ignoring: crmContactNilFieldIgnores(prefix: "contact").union(["contact.__entity_type"])
        )
    },
    RegisteredFixture(id: "native.crm.contact-email-draft.default") {
        try assertFixtureCoversDTO($0, as: CrmEmailDraftResponse.self)
    },
    RegisteredFixture(id: "native.crm.import.success.default") {
        try assertFixtureCoversDTO($0, as: CrmImportBatchResponse.self)
    },
    RegisteredFixture(id: "native.crm.import.validation.default") {
        try assertFixtureCoversDTO($0, as: CrmImportBatchResponse.self)
    }
]

private func crmContactNilFieldIgnores(prefix: String) -> Set<String> {
    [
        "\(prefix).archived_at",
        "\(prefix).assigned_to_id",
        "\(prefix).contacted_at",
        "\(prefix).converted_at",
        "\(prefix).metadata",
        "\(prefix).opted_out_at",
        "\(prefix).phone",
        "\(prefix).responded_at",
        "\(prefix).user_id"
    ]
}
