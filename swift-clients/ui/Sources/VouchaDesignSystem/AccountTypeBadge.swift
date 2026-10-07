import SwiftUI
import VouchaLocalization
import VouchaModels

/// Localized classification supplied by the account projection.
public struct AccountTypeBadge: View {
    @Environment(\.locale)
    private var locale

    public let accountType: AccountType?

    public init(accountType: AccountType?) {
        self.accountType = accountType
    }

    public var body: some View {
        if let accountType {
            Text(UiMessages.string(accountType.messageKey, locale: locale))
                .font(Typography.caption2)
                .foregroundStyle(Colors.secondaryLabel)
                .accessibilityIdentifier("account-type-\(accountType.rawValue)")
        }
    }
}

private extension AccountType {
    var messageKey: UiMessageKey {
        switch self {
        case .official: .sharedAccountTypeOfficial
        case .system: .sharedAccountTypeSystem
        case .aiAgent: .sharedAccountTypeAiAgent
        }
    }
}
