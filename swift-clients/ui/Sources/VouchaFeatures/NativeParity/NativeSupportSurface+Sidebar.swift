import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeSupportSurface {
    func supportSidebar(viewModel: NativeSupportViewModel) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            HStack {
                Text(UiMessages.string(.nativeSwiftSupportSupport, locale: nativeUiLocale))
                    .font(Typography.headline)
                Spacer()
                Button {
                    Task { await viewModel.startNewThread() }
                } label: {
                    Label(UiMessages.string(.nativeSwiftChatNew, locale: nativeUiLocale), systemImage: "plus")
                }
                .buttonStyle(.bordered)
            }

            if viewModel.isLoadingList,
               viewModel.threads.isEmpty {
                ProgressView()
                    .frame(maxWidth: .infinity, alignment: .center)
                    .padding(.vertical, Spacing.xl)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: Spacing.sm) {
                        ForEach(viewModel.threads) { thread in
                            supportThreadButton(thread, viewModel: viewModel)
                        }

                        HybridPaginationControl(
                            hasMore: viewModel.threadPagination.hasMore,
                            isLoading: viewModel.threadPagination.isLoading,
                            hasError: viewModel.threadPagination.lastError != nil
                        ) {
                            await viewModel.loadMoreThreads()
                        }
                    }
                }
            }

            if let message = viewModel.listErrorMessage {
                Text(UiMessages.string(message, locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.negativeVote)
            }
        }
        .frame(width: 280, alignment: .topLeading)
    }

    private func supportThreadButton(
        _ thread: SupportThread,
        viewModel: NativeSupportViewModel
    ) -> some View {
        Button {
            Task { await viewModel.selectThread(id: thread.id) }
        } label: {
            VStack(alignment: .leading, spacing: 2) {
                Text(thread.subject)
                    .font(Typography.subheadline)
                    .foregroundStyle(Colors.primary)
                Text(verbatim: UiMessages.string(supportThreadStatusText(thread.status), locale: nativeUiLocale))
                    .font(Typography.caption)
                    .foregroundStyle(Colors.secondaryLabel)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(Spacing.sm)
            .background(
                RoundedRectangle(cornerRadius: 8, style: .continuous)
                    .fill(viewModel.selectedThreadId == thread.id ? Colors.primary.opacity(0.15) : .clear)
            )
        }
        .buttonStyle(.plain)
    }
}
