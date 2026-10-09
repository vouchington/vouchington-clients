import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension NativeEngineeringQueuesSurface {
    func actionRow(
        icon: String,
        title: UiVerbatimText,
        detail: UiVerbatimText,
        buttonTitle: UiMessageKey = .nativeSwiftEngineeringPostgresqlRun,
        buttonIcon: String = "play.circle",
        isDisabled: Bool,
        action: @escaping () async -> Void
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            NativeSurfaceRow(row: .init(icon: icon, title: title, detail: detail))
            HStack {
                Spacer()
                Button {
                    Task { await action() }
                } label: {
                    Label(
                        UiMessages.string(buttonTitle, locale: nativeUiLocale),
                        systemImage: buttonIcon
                    )
                }
                .buttonStyle(.bordered)
                .disabled(isDisabled)
            }
        }
    }

    func queueStatsDetail(_ stats: EngineeringAggregatedQueueStats) -> UiMessage {
        UiMessage(
            .nativeSwiftEngineeringQueuesStatsSummary,
            numberParameters: [
                "waiting": Double(stats.totalWaiting),
                "delayed": Double(stats.totalDelayed),
                "active": Double(stats.totalActive),
                "failed": Double(stats.totalFailed),
                "queues": Double(stats.queueCount)
            ]
        )
    }

    func queueActionKey(_ queue: EngineeringQueueStats) -> String {
        queue.paused ? "resume|\(queue.name)" : "pause|\(queue.name)"
    }

    func queueDetail(_ queue: EngineeringQueueStats) -> UiMessage {
        UiMessage(
            .nativeSwiftEngineeringQueuesQueueSummary,
            numberParameters: [
                "waiting": Double(queue.waiting),
                "delayed": Double(queue.delayed),
                "active": Double(queue.active),
                "failed": Double(queue.failed),
                "completed": Double(queue.completed)
            ]
        )
    }
}
