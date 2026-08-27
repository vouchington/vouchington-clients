import VouchaModels

let staffSupportApiFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.staff-support.threads.default") {
        try assertFixtureCoversDTO($0, as: SupportThreadListResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.thread-detail.default") {
        try assertFixtureCoversDTO($0, as: SupportThreadResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.thread-assign.default") {
        try assertFixtureCoversDTO($0, as: SupportThreadResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.thread-resolve.default") {
        try assertFixtureCoversDTO($0, as: SupportThreadResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.thread-reopen.default") {
        try assertFixtureCoversDTO($0, as: SupportThreadResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.messages.default") {
        try assertFixtureCoversDTO($0, as: SupportMessageListResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.message-create.default") {
        try assertFixtureCoversDTO($0, as: SupportMessageResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.draft-create.default") {
        try assertFixtureCoversDTO($0, as: SupportDraftQueuedResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.message-edit.default") {
        try assertFixtureCoversDTO($0, as: SupportMessageResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.message-approve.default") {
        try assertFixtureCoversDTO($0, as: SupportMessageResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.message-send.default") {
        try assertFixtureCoversDTO($0, as: SupportMessageResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.contacts.default") {
        try assertFixtureCoversDTO($0, as: SupportContactListResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.contact-detail.default") {
        try assertFixtureCoversDTO($0, as: SupportContactDetailResponse.self)
    },
    RegisteredFixture(id: "native.staff-support.contact-update.default") {
        try assertFixtureCoversDTO($0, as: SupportContactResponse.self)
    }
]
