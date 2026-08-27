import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension NativeEngineeringValkeySurface {
    func flushSection(_ viewModel: NativeEngineeringValkeyViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            sectionTitle(UiMessages.string(.nativeSwiftEngineeringValkeyFlushConcerns, locale: nativeUiLocale))
            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(EngineeringValkeyFlushConcern.allCases, id: \.self) { concern in
                    VStack(alignment: .leading, spacing: Spacing.sm) {
                        NativeSurfaceRow(
                            row: .init(
                                icon: concern == .sessions ? "exclamationmark.triangle" : "bolt.slash",
                                title: .verbatim(concern.displayName),
                                detail: .app(UiMessage(
                                    .nativeSwiftEngineeringValkeyFilterIdentifier,
                                    parameters: ["value": concern.rawValue]
                                ))
                            )
                        )
                        HStack {
                            Spacer()
                            if concern == .sessions {
                                Button(role: .destructive) {
                                    pendingFlushConcern = concern
                                } label: {
                                    Label(
                                        UiMessages.string(
                                            .nativeSwiftEngineeringValkeyForceFlush,
                                            locale: nativeUiLocale
                                        ),
                                        systemImage: "exclamationmark.triangle.fill"
                                    )
                                }
                                .buttonStyle(.borderedProminent)
                                .tint(.red)
                                .disabled(viewModel.isActionLoading("flush|\(concern.rawValue)"))
                            } else {
                                Button {
                                    pendingFlushConcern = concern
                                } label: {
                                    Label(
                                        UiMessages.string(.nativeSwiftEngineeringValkeyFlush, locale: nativeUiLocale),
                                        systemImage: "bolt.slash"
                                    )
                                }
                                .buttonStyle(.bordered)
                                .disabled(viewModel.isActionLoading("flush|\(concern.rawValue)"))
                            }
                        }
                    }
                }
            }
        }
    }
}
