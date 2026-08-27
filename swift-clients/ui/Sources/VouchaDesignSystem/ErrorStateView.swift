import SwiftUI
import VouchaLocalization

public struct ErrorStateView: View {
    @Environment(\.locale)
    private var locale
    public let error: any LocalizedError
    public let retry: () async -> Void

    public init(error: any LocalizedError, retry: @escaping () async -> Void) {
        self.error = error
        self.retry = retry
    }

    public var body: some View {
        VStack(spacing: Spacing.md) {
            Image(systemName: "exclamationmark.triangle")
                .font(.system(size: 48))
                .foregroundStyle(Colors.negativeVote)

            Text(UiMessages.string(.nativeCommonSomethingWentWrong, locale: locale))
                .font(Typography.headline)

            if let description = error.errorDescription {
                Text(description)
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
                    .multilineTextAlignment(.center)
            }

            Button(UiMessages.string(.nativeCommonRetry, locale: locale)) {
                Task { await retry() }
            }
            .buttonStyle(.borderedProminent)
            .padding(.top, Spacing.sm)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .padding(Spacing.xl)
    }
}
