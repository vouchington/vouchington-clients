import SwiftUI
import VouchaAPI
import VouchaDesignSystem
import VouchaLocalization

extension GrowthDashboardSurface {
    func metricsContent(_ metrics: GrowthMetrics) -> some View {
        VStack(alignment: .leading, spacing: Spacing.md) {
            userMetrics(metrics)
            contentMetrics(metrics)
            engagementMetrics(metrics)
            networkMetrics(metrics)
            revenueMetrics(metrics)
            infrastructureMetrics(metrics)
        }
    }

    @ViewBuilder
    private func userMetrics(_ metrics: GrowthMetrics) -> some View {
        GrowthMetricSection(title: appText(.nativeDotnetGrowthUsers), rows: [
            .init(
                appText(.nativeDotnetGrowthTotalUsers),
                count(metrics.userGrowth.totalUsers),
                appText(.nativeDotnetGrowthRegisteredAccounts)
            ),
            .init(
                appText(.nativeDotnetGrowthNewUsers),
                count(metrics.userGrowth.newUsers),
                appText(.nativeDotnetGrowthSelectedRange)
            ),
            .init(
                appText(.nativeDotnetGrowthDau),
                count(metrics.userGrowth.dau),
                appText(.nativeDotnetGrowthDailyActiveUsers)
            ),
            .init(
                appText(.nativeDotnetGrowthMau),
                count(metrics.userGrowth.mau),
                appText(.nativeDotnetGrowthMonthlyActiveUsers)
            ),
            .init(
                appText(.nativeDotnetGrowthDauMau),
                percent(metrics.userGrowth.dauMauRatio),
                appText(.nativeDotnetGrowthStickinessRatio)
            )
        ])
        GrowthMiniBars(
            title: appText(.nativeDotnetGrowthDailySignups),
            values: metrics.userGrowth.signupsOverTime.map(\.count)
        )
    }

    private func contentMetrics(_ metrics: GrowthMetrics) -> some View {
        GrowthMetricSection(title: appText(.nativeDotnetGrowthContent), rows: [
            .init(
                appText(.nativeDotnetGrowthTotalPosts),
                count(metrics.contentProduction.totalPosts),
                appText(.nativeDotnetGrowthPublishedPosts)
            ),
            .init(
                appText(.nativeDotnetGrowthReviews),
                count(metrics.contentProduction.postsByType.review),
                appText(.nativeDotnetGrowthReviewPosts)
            ),
            .init(
                appText(.nativeDotnetGrowthDataPoints),
                count(metrics.contentProduction.postsByType.dataPoint),
                appText(.nativeDotnetGrowthDataPointPosts)
            ),
            .init(
                appText(.nativeSwiftRouteSurfaceStories),
                count(metrics.contentProduction.postsByType.story),
                appText(.nativeDotnetGrowthStoryPosts)
            ),
            .init(
                appText(.nativeDotnetGrowthApprovalRate),
                percent(metrics.contentProduction.clearanceApprovalRate),
                appText(.nativeDotnetGrowthClearanceWorkflow)
            )
        ])
    }

    private func engagementMetrics(_ metrics: GrowthMetrics) -> some View {
        GrowthMetricSection(title: appText(.nativeDotnetGrowthEngagement), rows: [
            .init(
                appText(.nativeDotnetGrowthVotes),
                count(metrics.engagement.votesCast),
                appText(.nativeDotnetGrowthVoteVolume)
            ),
            .init(
                appText(.nativeDotnetGrowthComments),
                count(metrics.engagement.commentsCreated),
                appText(.nativeDotnetGrowthConversationVolume)
            ),
            .init(
                appText(.nativeDotnetGrowthFollows),
                count(metrics.engagement.followsCreated),
                appText(.nativeDotnetGrowthNewFollows)
            ),
            .init(
                appText(.nativeDotnetGrowthAvgFollowsUser),
                decimal(metrics.engagement.avgFollowsPerUser),
                appText(.nativeDotnetGrowthNetworkDensity)
            )
        ])
    }

    private func networkMetrics(_ metrics: GrowthMetrics) -> some View {
        GrowthMetricSection(title: appText(.nativeDotnetGrowthNetwork), rows: [
            .init(
                appText(.nativeDotnetGrowthReferralCoefficient),
                decimal(metrics.networkEffects.referralCoefficient),
                appText(.nativeDotnetGrowthReferralLoop)
            ),
            .init(
                appText(.nativeDotnetGrowthTopicCoverage),
                percent(metrics.networkEffects.topicCoverageRate),
                appText(.nativeDotnetGrowthCoveredTopics)
            ),
            .init(
                appText(.nativeDotnetGrowthLandingPageVisits),
                count(metrics.networkEffects.landingPageVisits),
                appText(.nativeDotnetGrowthProfileReach)
            ),
            .init(
                appText(.nativeDotnetGrowthSignupVisit),
                percent(metrics.networkEffects.signupVisitRatio),
                appText(.nativeDotnetGrowthVisitConversion)
            )
        ])
    }

    private func revenueMetrics(_ metrics: GrowthMetrics) -> some View {
        GrowthMetricSection(title: appText(.nativeDotnetGrowthRevenue), rows: [
            .init(
                appText(.nativeDotnetGrowthActiveMemberships),
                count(metrics.revenue.activeMemberships),
                appText(.nativeDotnetGrowthActivePaidAccounts)
            ),
            .init(
                appText(.nativeDotnetGrowthMrr),
                moneyList(metrics.revenue.mrrByCurrency),
                appText(.nativeDotnetGrowthMonthlyRevenue)
            ),
            .init(
                appText(.nativeDotnetGrowthUpgrades),
                count(metrics.revenue.upgrades),
                appText(.nativeDotnetGrowthPlanUpgrades)
            ),
            .init(
                appText(.nativeDotnetGrowthChurn),
                percent(metrics.revenue.churnRate),
                appText(.nativeDotnetGrowthCancellationRate)
            )
        ])
    }

    private func infrastructureMetrics(_ metrics: GrowthMetrics) -> some View {
        GrowthMetricSection(title: appText(.nativeDotnetGrowthInfrastructure), rows: [
            .init(
                appText(.nativeDotnetGrowthCrawlerSuccess),
                percent(metrics.infrastructure.crawlerSuccessRate),
                appText(.nativeDotnetGrowthFeedCrawlerSuccess)
            ),
            .init(
                appText(.nativeDotnetGrowthQueueThroughput),
                count(metrics.infrastructure.queueThroughput),
                appText(.nativeDotnetGrowthJobsProcessed)
            ),
            .init(
                appText(.nativeDotnetGrowthCacheHitRate),
                percent(metrics.infrastructure.cacheHitRate),
                appText(.nativeDotnetGrowthCacheEfficiency)
            ),
            .init(
                appText(.nativeDotnetGrowthAiTokenUsage),
                count(metrics.infrastructure.aiTokenUsage),
                appText(.nativeDotnetGrowthModelTokens)
            )
        ])
    }

}
