import VouchaLocalization
import VouchaModels

extension CrmContactStatus {
    var titleKey: UiMessageKey {
        switch self {
        case .new:
            .nativeSwiftCrmContactsContactNew
        case .awaitingResponse:
            .nativeSwiftCrmContactsAwaitingResponse
        case .inConversation:
            .nativeSwiftCrmContactsInConversation
        case .converted:
            .nativeSwiftCrmContactsContactConverted
        case .archived:
            .nativeSwiftCrmContactsContactArchived
        case .optedOut:
            .nativeSwiftCrmContactsContactOptedOut
        }
    }
}

extension CrmContactVertical {
    var titleKey: UiMessageKey {
        switch self {
        case .creditCards: .nativeSwiftCrmContactsCreditCards
        case .travel: .nativeSwiftCrmContactsTravel
        case .cars: .nativeSwiftCrmContactsCars
        case .artificialIntelligence: .nativeSwiftCrmContactsArtificialIntelligence
        case .technology: .nativeSwiftCrmContactsTechnology
        case .finance: .nativeSwiftCrmContactsFinance
        case .lifestyle: .nativeSwiftCrmContactsLifestyle
        case .other: .nativeSwiftCrmContactsOther
        }
    }
}

extension CrmContactType {
    var titleKey: UiMessageKey {
        switch self {
        case .influencer: .nativeSwiftCrmContactsContactInfluencer
        case .customer: .nativeSwiftCrmContactsContactCustomer
        case .partner: .nativeSwiftCrmContactsContactPartner
        }
    }
}

extension CrmSocialPlatform {
    var titleText: UiVerbatimText {
        switch self {
        case .instagram: .externalProvider("Instagram")
        case .tiktok: .externalProvider("TikTok")
        case .youtube: .externalProvider("YouTube")
        case .xPlatform: .externalProvider("X")
        case .linkedin: .externalProvider("LinkedIn")
        }
    }
}

extension CrmEmailProvider {
    var titleText: UiVerbatimText {
        switch self {
        case .ses:
            .externalProvider("SES")
        case .gmailSmtp:
            .externalProvider("Gmail SMTP")
        }
    }
}

extension CRMContactsViewModel.LinkedFilter {
    var titleKey: UiMessageKey {
        switch self {
        case .all:
            .nativeSwiftCrmContactsLinkedFilterAll
        case .linked:
            .nativeSwiftCrmContactsLinkedFilterLinked
        case .unlinked:
            .nativeSwiftCrmContactsLinkedFilterUnlinked
        }
    }
}
