import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension NativeEngineeringValkeySurface {
    func cacheSection(_ viewModel: NativeEngineeringValkeyViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            HStack {
                sectionTitle(UiMessages.string(.nativeSwiftEngineeringValkeyCacheManagement, locale: nativeUiLocale))
                Spacer()
                Button(role: .destructive) {
                    viewModel.pendingAction = .clearAll
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftEngineeringValkeyClearAll, locale: nativeUiLocale),
                        systemImage: "trash"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isActionLoading("clear|all"))
            }

            LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                ForEach(viewModel.cacheGroups) { group in
                    cacheGroup(group, viewModel: viewModel)
                }
            }
        }
    }

    private func cacheGroup(
        _ group: EngineeringValkeyCacheGroup,
        viewModel: NativeEngineeringValkeyViewModel
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            NativeSurfaceRow(
                row: .init(
                    icon: "externaldrive.connected.to.line.below",
                    title: .verbatim(group.name),
                    detail: .count(group.prefixes.count, item: "prefix")
                )
            )
            Text(group.prefixes.joined(separator: ", "))
                .font(.caption)
                .foregroundStyle(Colors.secondaryLabel)
            HStack {
                Spacer()
                Button(role: .destructive) {
                    viewModel.pendingAction = .clearGroup(group.name)
                } label: {
                    Label(
                        UiMessages.string(.nativeSwiftCommonClear, locale: nativeUiLocale),
                        systemImage: "trash"
                    )
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isActionLoading("clear|\(group.name)"))
            }
        }
    }
}
