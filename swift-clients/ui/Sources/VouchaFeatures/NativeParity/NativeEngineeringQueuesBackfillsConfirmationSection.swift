import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct BackfillsConfirmationSection: View {
    @Environment(\.locale)
    var nativeUiLocale
    let viewModel: NativeEngineeringQueuesViewModel
    @State
    private var pendingBackfill: EngineeringBackfill?

    var body: some View {
        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.backfills) { backfill in
                backfillRow(
                    backfill,
                    isDisabled: viewModel.isActionLoading("backfill|\(backfill.id)")
                ) {
                    pendingBackfill = backfill
                }
            }
        }
        .confirmationDialog(
            UiMessages.string(.nativeSwiftEngineeringQueuesRunBackfillConfirmationTitle, locale: nativeUiLocale),
            isPresented: Binding(
                get: { pendingBackfill != nil },
                set: {
                    if !$0 {
                        pendingBackfill = nil
                    }
                }
            ),
            titleVisibility: .visible
        ) {
            Button(
                UiMessages.string(.nativeSwiftEngineeringQueuesRunBackfill, locale: nativeUiLocale),
                role: .destructive
            ) {
                let backfill = pendingBackfill
                pendingBackfill = nil
                guard let backfill else { return }
                Task { await viewModel.runBackfill(id: backfill.id) }
            }
            Button(UiMessages.string(.nativeSwiftCommonCancel, locale: nativeUiLocale), role: .cancel) {
                pendingBackfill = nil
            }
        } message: {
            Text(backfillMessage(for: pendingBackfill))
        }
    }

    private func backfillMessage(for backfill: EngineeringBackfill?) -> String {
        guard let backfill else { return "" }
        return UiMessages.string(
            .nativeSwiftEngineeringQueuesRunBackfillMessage,
            parameters: ["id": UiMessages.string(.verbatim(backfill.id), locale: nativeUiLocale)],
            locale: nativeUiLocale
        )
    }

    private func backfillRow(
        _ backfill: EngineeringBackfill,
        isDisabled: Bool,
        action: @escaping () async -> Void
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            NativeSurfaceRow(row: .init(
                icon: "arrow.clockwise",
                title: .verbatim(backfill.id),
                detail: .verbatim(backfill.description)
            ))
            HStack {
                Spacer()
                Button {
                    Task { await action() }
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftEngineeringQueuesRunBackfill, locale: nativeUiLocale),
                        systemImage: "arrow.clockwise"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(isDisabled)
            }
        }
    }
}
