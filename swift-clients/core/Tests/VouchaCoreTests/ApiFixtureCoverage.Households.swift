import VouchaModels

let householdFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.households.empty") {
        try assertFixtureCoversDTO($0, as: HouseholdPage.self)
    },
    RegisteredFixture(id: "native.households.single-owned") {
        try assertFixtureCoversDTO($0, as: HouseholdPage.self, ignoring: ["results.created_at"])
    },
    RegisteredFixture(id: "native.households.multiple") {
        try assertFixtureCoversDTO($0, as: HouseholdPage.self, ignoring: ["results.created_at"])
    },
    RegisteredFixture(id: "native.households.owned") {
        try assertFixtureCoversDTO($0, as: HouseholdPage.self, ignoring: ["results.created_at"])
    },
    RegisteredFixture(id: "native.households.member.default") {
        try assertFixtureCoversDTO($0, as: HouseholdPage.self, ignoring: ["results.created_at"])
    },
    RegisteredFixture(id: "native.households.member.page-2") {
        try assertFixtureCoversDTO($0, as: HouseholdPage.self, ignoring: ["results.created_at"])
    },
    RegisteredFixture(id: "native.household-memberships.empty") {
        try assertFixtureCoversDTO($0, as: HouseholdMembershipPage.self)
    },
    RegisteredFixture(id: "native.household-memberships.single") {
        try assertFixtureCoversDTO($0, as: HouseholdMembershipPage.self)
    },
    RegisteredFixture(id: "native.household-memberships.multiple") {
        try assertFixtureCoversDTO($0, as: HouseholdMembershipPage.self)
    },
    RegisteredFixture(id: "native.household-memberships.page-1") {
        try assertFixtureCoversDTO($0, as: HouseholdMembershipPage.self)
    },
    RegisteredFixture(id: "native.household-memberships.page-2") {
        try assertFixtureCoversDTO(
            $0,
            as: HouseholdMembershipPage.self,
            ignoring: ["results.individual.user_id", "results.individual.username"]
        )
    },
    RegisteredFixture(id: "native.households.create.default") {
        try assertFixtureCoversDTO($0, as: HouseholdCreateResponse.self)
    }
]
