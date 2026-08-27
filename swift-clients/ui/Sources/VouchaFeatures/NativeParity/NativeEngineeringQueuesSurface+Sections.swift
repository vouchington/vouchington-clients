import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension NativeEngineeringQueuesSurface {
    @ViewBuilder
    func loadedContent(_ viewModel: NativeEngineeringQueuesViewModel) -> some View {
        emptyQueuesView(viewModel)
        statsSection(viewModel.stats)
        sectionTitle(UiMessages.string(.nativeSwiftEngineeringQueuesQueues, locale: nativeUiLocale))
        queueSection(viewModel)
        sectionTitle(UiMessages.string(.nativeSwiftEngineeringQueuesScheduledJobs, locale: nativeUiLocale))
        scheduledJobsSection(viewModel)
        sectionTitle(UiMessages.string(.nativeSwiftEngineeringQueuesBackfills, locale: nativeUiLocale))
        backfillsSection(viewModel)
    }

    @ViewBuilder
    private func emptyQueuesView(_ viewModel: NativeEngineeringQueuesViewModel) -> some View {
        if viewModel.queues.isEmpty {
            EmptyStateView(
                icon: "tray.full",
                title: .message(.nativeSwiftEmptyStateNoQueues),
                message: .message(.nativeSwiftEmptyStateNoQueuesMessage)
            )
        }
    }

    @ViewBuilder
    private func statsSection(_ stats: EngineeringAggregatedQueueStats?) -> some View {
        if let stats {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                sectionTitle(UiMessages.string(.nativeSwiftEngineeringQueuesQueueStats, locale: nativeUiLocale))
                Text(UiMessages.string(queueStatsDetail(stats), locale: nativeUiLocale))
                    .font(.subheadline)
                    .foregroundStyle(Colors.secondaryLabel)
            }
        }
    }

    private func queueSection(_ viewModel: NativeEngineeringQueuesViewModel) -> some View {
        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.queues, id: \.name) { queue in
                queueRow(queue, viewModel: viewModel)
            }
        }
    }

    private func queueRow(
        _ queue: EngineeringQueueStats,
        viewModel: NativeEngineeringQueuesViewModel
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            NativeSurfaceRow(
                row: .init(
                    icon: queue.paused ? "pause.circle" : "shippingbox",
                    title: .verbatim(queue.name),
                    detail: .app(queueDetail(queue))
                )
            )
            HStack {
                Spacer()
                Button(UiMessages.string(
                    queue.paused
                        ? .nativeSwiftEngineeringQueuesResume
                        : .nativeSwiftEngineeringQueuesPause,
                    locale: nativeUiLocale
                )) {
                    Task {
                        queue.paused ? await viewModel.resumeQueue(name: queue.name)
                            : await viewModel.pauseQueue(name: queue.name)
                    }
                }
                .buttonStyle(.bordered)
                .disabled(viewModel.isActionLoading(queueActionKey(queue)))
            }
        }
    }

    private func scheduledJobsSection(_ viewModel: NativeEngineeringQueuesViewModel) -> some View {
        LazyVStack(alignment: .leading, spacing: Spacing.sm) {
            ForEach(viewModel.scheduledJobs) { job in
                actionRow(
                    icon: "clock.arrow.circlepath",
                    title: .verbatim(job.id),
                    detail: .verbatim(job.description),
                    isDisabled: viewModel.isActionLoading("scheduled|\(job.id)")
                ) {
                    await viewModel.runScheduledJob(id: job.id)
                }
            }
        }
    }

    private func backfillsSection(_ viewModel: NativeEngineeringQueuesViewModel) -> some View {
        BackfillsConfirmationSection(viewModel: viewModel)
    }
}
