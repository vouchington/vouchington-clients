@testable import VouchaAPI

let copyrightNoticesFixtureEndpoints: [String: Endpoint] = [
    "web.copyright.notices.default": .copyrightNotices(limit: nil),
    "web.copyright.notices.null-claimant": .copyrightNotices(limit: nil),
    "web.copyright.notice.detail.populated": .copyrightNotice(
        id: "00000000-0000-7000-8000-000000000804"
    ),
    "web.copyright.notice.detail.null-claimant": .copyrightNotice(
        id: "00000000-0000-7000-8000-000000000804"
    ),
    "web.copyright.notice.participant.populated": .copyrightParticipantNotice(
        id: "00000000-0000-7000-8000-000000000804"
    ),
    "web.copyright.notice.participant.null-claimant": .copyrightParticipantNotice(
        id: "00000000-0000-7000-8000-000000000804"
    ),
    "web.copyright.eu.participant.no-action-complaint": .copyrightParticipantNotice(
        id: "00000000-0000-7000-8000-000000001218"
    ),
    "web.copyright.eu.dispute-settlements.participant": .copyrightEuDisputeSettlements(
        id: "00000000-0000-7000-8000-000000001218"
    )
]
