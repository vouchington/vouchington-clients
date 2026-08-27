import SwiftUI
import VouchaDesignSystem
import VouchaLocalization

extension StaffSupportSurface {
    func staffMessageHeader(senderKey: UiMessageKey, createdAt: Date) -> some View {
        HStack {
            Text(UiMessages.string(senderKey, locale: nativeUiLocale))
                .font(Typography.caption)
                .foregroundStyle(Colors.secondaryLabel)
            Spacer(minLength: 0)
            Text(UiMessages.date(
                createdAt,
                date: .abbreviated,
                time: .shortened,
                locale: nativeUiLocale,
                timeZone: .current
            ))
            .font(Typography.caption)
            .foregroundStyle(Colors.secondaryLabel)
        }
    }
}
