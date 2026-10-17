import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

struct NativeEngineeringQueueDomainsSection: View {
    @Environment(\.locale)
    private var locale
    let viewModel: NativeEngineeringQueuesViewModel

    var body: some View {
        if !viewModel.domains.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.md) {
                Text(UiMessages.string(.extractedQueuesDomainsSectionBackgroundDomainsD956d6be, locale: locale))
                    .font(.headline)
                ForEach(viewModel.domains, id: \.domain) { domain in
                    domainRow(domain)
                }
            }
        }
    }

    private func domainRow(_ domain: EngineeringQueueDomain) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(verbatim: domain.domain).font(.headline)
            Text(UiMessages.string(status(domain), locale: locale))
            HStack(alignment: .top) {
                count(.extractedQueuesDomainsSectionWaiting6e293a8c, domain.waiting)
                count(.extractedQueuesDomainsSectionActive92340695, domain.active)
                count(.extractedQueuesDomainsSectionDelayedA5c19c9f, domain.delayed)
                count(.extractedQueuesDomainsSectionFailed031a8f0f, domain.failed)
                count(.extractedQueuesDomainsSectionCompleted22a970d2, domain.completed)
            }
            HStack {
                Button(UiMessages.string(
                    domain
                        .paused ? .extractedQueuesDomainsSectionResumeD640c742 :
                        .extractedQueuesDomainsSectionPause858e4ba7,
                    locale: locale
                )) {
                    Task {
                        if domain.paused {
                            await viewModel.resumeDomain(domain.domain)
                        } else {
                            await viewModel.pauseDomain(domain.domain)
                        }
                    }
                }
                Button(UiMessages.string(.extractedQueuesDomainsSectionRetryFailed2630f650, locale: locale)) {
                    Task { await viewModel.retryFailedDomain(domain.domain) }
                }
            }
            .buttonStyle(.bordered)
            .disabled(viewModel.isActionLoading("domain|\(domain.domain)"))
        }
    }

    private func count(_ key: UiMessageKey, _ value: EngineeringDomainStateCount) -> some View {
        VStack(alignment: .leading) {
            Text(UiMessages.string(key, locale: locale)).font(.caption)
            Text(verbatim: UiMessages.queueDomainCount(value.count, complete: value.complete, locale: locale))
        }
    }

    private func status(_ domain: EngineeringQueueDomain) -> UiMessage {
        guard domain.paused else { return UiMessage(.extractedQueuesDomainsSectionRunningF4ccae29) }
        if let reason = domain.reason {
            return UiMessage(
                .extractedQueuesDomainsSectionPausedReason41440167,
                textParameters: ["reason": .verbatim(reason)]
            )
        }
        return UiMessage(.extractedQueuesDomainsSectionPausedE159b061)
    }
}
