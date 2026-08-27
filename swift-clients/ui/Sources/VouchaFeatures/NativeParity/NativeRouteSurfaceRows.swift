import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeActionSurface: View {
    @Bindable
    var viewModel: NativeRouteSurfaceViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.actions) { action in
                Button {
                    Task { await viewModel.perform(action: action) }
                } label: {
                    NativeSurfaceRow(
                        row: .init(
                            icon: action.icon,
                            title: .app(action.title),
                            detail: .app(action.detail)
                        )
                    )
                }
                .buttonStyle(.plain)
                .disabled(viewModel.isLoading)
            }
        }
    }
}

@MainActor
func nativeRetryAction(for viewModel: NativeRouteSurfaceViewModel) -> (@Sendable () async -> Void)? {
    guard viewModel.destination?.supportsRemoteNativeSurface == true else { return nil }
    return { await viewModel.load() }
}

struct NativeRowsSurface: View {
    let rows: [NativeRouteDestinationRow]
    let state: LoadState
    let retry: (@Sendable () async -> Void)?
    var onNavigate: ((String) -> Void)?

    var body: some View {
        switch state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, alignment: .center)
                .padding(.vertical, Spacing.xl)
        case let .error(error):
            if let retry {
                ErrorStateView(error: error) {
                    await retry()
                }
            } else {
                EmptyStateView(
                    icon: "exclamationmark.triangle",
                    title: .message(.nativeSwiftEmptyStateUnableToLoad),
                    message: error.errorDescription.map { .verbatim($0) }
                )
            }
        default:
            if rows.isEmpty {
                EmptyStateView(
                    icon: "square.stack.3d.down.right",
                    title: .message(.nativeSwiftEmptyStateNoNativeRows),
                    message: .message(.nativeSwiftEmptyStateNoNativeRowsMessage)
                )
            } else {
                LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                    ForEach(rows) { row in
                        if let targetPath = row.targetPath, let onNavigate {
                            Button { onNavigate(targetPath) } label: {
                                NativeSurfaceRow(row: row)
                            }
                            .buttonStyle(.plain)
                        } else {
                            NativeSurfaceRow(row: row)
                        }
                    }
                }
            }
        }
    }
}
