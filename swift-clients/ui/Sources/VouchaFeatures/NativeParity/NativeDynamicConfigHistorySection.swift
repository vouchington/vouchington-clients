import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeDynamicConfigHistorySection: View {
    @Environment(\.locale)
    private var nativeUiLocale
    let history: [DynamicConfigHistoryEntry]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsHistory, locale: nativeUiLocale))
                .font(Typography.largeTitle)
            if history.isEmpty {
                Text(UiMessages.string(
                    .nativeSwiftDynamicConfigFeatureFlagsNoChangesRecorded,
                    locale: nativeUiLocale
                )).foregroundStyle(.secondary)
            }
            ForEach(history) { entry in
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(actorName(entry)).font(Typography.headline)
                    Text(UiMessages.date(
                        entry.createdAt,
                        date: .abbreviated,
                        time: .shortened,
                        locale: nativeUiLocale,
                        timeZone: .current
                    ))
                    .font(Typography.caption).foregroundStyle(.secondary)
                    ForEach(entry.changedFields.keys.sorted(), id: \.self) { name in
                        if let change = entry.changedFields[name] {
                            Text(UiMessages.string(
                                .nativeSwiftDynamicConfigFeatureFlagsHistoryChange,
                                parameters: [
                                    "name": name,
                                    "previous": change.previous.displayValue,
                                    "next": change.next.displayValue
                                ],
                                locale: nativeUiLocale
                            ))
                            .font(Typography.subheadline.monospaced())
                        }
                    }
                }
                .padding(.vertical, Spacing.sm)
            }
        }
    }

    private func actorName(_ entry: DynamicConfigHistoryEntry) -> String {
        if let username = entry.changedBy?.username {
            return username
        }
        if let id = entry.changedBy?.id {
            return UiMessages.string(
                .nativeSwiftDynamicConfigFeatureFlagsUser,
                parameters: ["id": id],
                locale: nativeUiLocale
            )
        }
        return UiMessages.string(.nativeSwiftDynamicConfigFeatureFlagsDeletedUser, locale: nativeUiLocale)
    }
}
