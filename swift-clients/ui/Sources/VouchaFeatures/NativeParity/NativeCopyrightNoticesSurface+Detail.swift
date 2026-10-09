import SwiftUI
import VouchaDesignSystem
import VouchaLocalization
import VouchaModels

extension NativeCopyrightNoticesSurface {
    func detail(noticeId: String) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.md) {
                backButton
                if let notice {
                    noticeContent(notice)
                } else if isLoading {
                    ProgressView()
                }
                detailError
            }
            .padding(Spacing.md)
        }
        .accessibilityIdentifier("copyright-notice-detail-\(noticeId)")
    }

    @ViewBuilder
    private func noticeContent(_ notice: CopyrightNotice) -> some View {
        Text(localized(.nativeCopyrightNoticesCase, parameters: ["id": notice.id]))
            .foregroundStyle(.secondary)
        Text(localized(notice.jurisdiction == .euDSA
                ? .nativeCopyrightNoticesEuDetailTitle
                : .nativeCopyrightNoticesDetailTitle))
            .font(Typography.largeTitle)
        receivedDate(notice)
        if notice.jurisdiction != .euDSA { claimant(notice.claimant) }
        targetSection(notice)
        timelineSection(notice)
        statementsSection
        if let euCase = notice.eu { euDecision(euCase) }
    }

    private var backButton: some View {
        Button(localized(.nativeCopyrightNoticesTitle)) {
            onNavigateToTargetPath("/copyright/notices")
        }
        .buttonStyle(.plain)
        .accessibilityIdentifier("copyright-notices-back")
    }

    @ViewBuilder
    private func receivedDate(_ notice: CopyrightNotice) -> some View {
        if notice.jurisdiction == .euDSA {
            Text(localized(.nativeCopyrightNoticesReceivedDate, parameters: [
                "date": localizedDate(notice.receivedAt, style: .date)
            ]))
        } else if let acceptedAt = notice.acceptedAt {
            Text(localized(.nativeCopyrightNoticesAcceptedDate, parameters: [
                "date": localizedDate(acceptedAt, style: .date)
            ]))
        }
    }

    @ViewBuilder
    private func targetSection(_ notice: CopyrightNotice) -> some View {
        if let targets = notice.targets, !targets.isEmpty || notice.jurisdiction != .euDSA {
            section(.nativeCopyrightNoticesAffectedHostedMaterial) {
                ForEach(targets) { target in targetRow(target, notice: notice) }
            }
        }
    }

    private func targetRow(_ target: CopyrightNoticeTarget, notice: CopyrightNotice) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            if notice.jurisdiction != .euDSA {
                Text(surfaceLabel(target.surface)).font(Typography.headline)
            }
            if let url = target.hostedUseUrl {
                hostedUseLink(url)
            } else {
                Text(localized(.nativeCopyrightNoticesHostedMaterialUnavailable))
            }
            Text(localized(.nativeCopyrightNoticesStatus, parameters: ["status": target.restrictionStatus]))
                .foregroundStyle(.secondary)
        }
        .padding(Spacing.sm)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 8))
    }

    @ViewBuilder
    private func timelineSection(_ notice: CopyrightNotice) -> some View {
        if notice.jurisdiction != .euDSA, let timeline = notice.timeline {
            section(.nativeCopyrightNoticesCaseTimeline) {
                ForEach(timeline) { event in
                    Text(UiMessages.string(
                        .joined([
                            timelineMessage(event.eventType),
                            .protocolValue(localizedDate(event.createdAt, style: .timestamp))
                        ], separator: ". "),
                        locale: nativeUiLocale
                    ))
                }
            }
        }
    }

    @ViewBuilder
    private var statementsSection: some View {
        if let statements = participantNotice?.statements, !statements.isEmpty {
            section(.nativeCopyrightNoticesStatementsAndDecisions) {
                ForEach(statements) { statement in
                    VStack(alignment: .leading, spacing: Spacing.xs) {
                        Text(statementDeliveryLabel(statement)).foregroundStyle(.secondary)
                        Text(statement.text).textSelection(.enabled)
                    }
                    .padding(Spacing.sm)
                }
            }
        }
    }

    @ViewBuilder
    private var detailError: some View {
        if let errorMessage {
            Text(UiMessages.string(errorMessage, locale: nativeUiLocale)).foregroundStyle(.red)
            Button(localized(.nativeCommonRetry)) {
                Task { await viewModel.loadInitial() }
            }
            .accessibilityIdentifier("copyright-notices-initial-retry")
        }
    }

    func section(
        _ title: UiMessageKey,
        @ViewBuilder content: () -> some View
    ) -> some View {
        VStack(alignment: .leading, spacing: Spacing.sm) {
            Text(localized(title)).font(Typography.headline)
            content()
        }
    }

    @ViewBuilder
    func claimant(_ value: CopyrightNoticeClaimant?) -> some View {
        if let value {
            HStack(spacing: Spacing.xs) {
                Text(localized(.nativeCopyrightNoticesClaimant) + ":")
                    .foregroundStyle(.secondary)
                Button(value.displayName) {
                    onNavigateToTargetPath("/user/\(pathComponent(value.userId))")
                }
                .underline()
                .buttonStyle(.plain)
                .accessibilityIdentifier("copyright-notice-claimant-\(value.userId)")
            }
        }
    }

}
