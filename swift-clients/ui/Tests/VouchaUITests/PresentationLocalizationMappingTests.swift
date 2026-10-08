@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

final class PresentationLocalizationMappingTests: XCTestCase {
    func testTaxonomyEnumsMapEveryCaseToTypedKeys() {
        assertMappings([
            (ListVisibility.private.titleKey, .nativeTaxonomyListsPrivate),
            (ListVisibility.unlisted.titleKey, .nativeTaxonomyListsUnlisted),
            (ListVisibility.public.titleKey, .nativeTaxonomyListsPublic),
            (CommunityVisibility.public.titleKey, .nativeTaxonomyListsPublic),
            (CommunityVisibility.private.titleKey, .nativeTaxonomyListsPrivate),
            (CommunityMemberRosterVisibility.public.titleKey, .nativeTaxonomyListsPublic),
            (CommunityMemberRosterVisibility.users.titleKey, .nativeTaxonomyCommunityUsers),
            (CommunityMemberRosterVisibility.members.titleKey, .nativeTaxonomyCommunityMembers),
            (CommunityMemberRosterVisibility.moderators.titleKey, .nativeTaxonomyCommunityModerators),
            (CommunityListType.follow.titleKey, .nativeTaxonomyCommunityFollow),
            (CommunityListType.mute.titleKey, .nativeTaxonomyCommunityMute),
            (
                CommunityRestrictionType.requirePostApproval.titleKey,
                .nativeTaxonomyCommunityRequirePostApproval
            ),
            (
                CommunityRestrictionType.noNewMemberPosts.titleKey,
                .nativeTaxonomyCommunityNoNewMemberPosts
            ),
            (CommunityRestrictionType.noLinks.titleKey, .nativeTaxonomyCommunityNoLinks),
            (
                CommunityRestrictionType.approvedMembersOnly.titleKey,
                .nativeTaxonomyCommunityApprovedMembersOnly
            ),
            (
                CommunityAutomodActionCurrentState.rejected.titleKey,
                .nativeTaxonomyModerationRejected
            ),
            (
                CommunityAutomodActionCurrentState.inReview.titleKey,
                .nativeTaxonomyModerationInReview
            ),
            (
                CommunityAutomodActionCurrentState.unpublished.titleKey,
                .nativeTaxonomyModerationUnpublished
            ),
            (DataPointVertical.creditCard.titleKey, .nativeTaxonomyDataPointCreditCard),
            (DataPointVertical.bankAccount.titleKey, .nativeTaxonomyDataPointBankAccount)
        ])
    }

    func testWorkflowStatusEnumsMapEveryCaseToTypedKeys() {
        assertMappings([
            (DataRequestStatus.pending.titleKey, .nativeTaxonomyModerationPending),
            (DataRequestStatus.processing.titleKey, .nativeTaxonomyDataRequestProcessing),
            (DataRequestStatus.ready.titleKey, .nativeTaxonomyDataRequestReady),
            (DataRequestStatus.failed.titleKey, .nativeTaxonomyDataRequestFailed),
            (DataRequestStatus.expired.titleKey, .nativeTaxonomyDataRequestExpired),
            (ModerationAppealStatus.pending.titleKey, .nativeTaxonomyModerationPending),
            (ModerationAppealStatus.dismissed.titleKey, .nativeTaxonomyModerationDismissed),
            (ModerationAppealStatus.resolved.titleKey, .nativeTaxonomyModerationResolved),
            (ModerationDisputeStatus.pending.titleKey, .nativeTaxonomyModerationPending),
            (ModerationDisputeStatus.dismissed.titleKey, .nativeTaxonomyModerationDismissed),
            (ModerationDisputeStatus.resolved.titleKey, .nativeTaxonomyModerationResolved),
            (ReviewDisputeStatus.pending.titleKey, .nativeTaxonomyModerationPending),
            (ReviewDisputeStatus.resolved.titleKey, .nativeTaxonomyModerationResolved),
            (ReviewDisputeStatus.dismissed.titleKey, .nativeTaxonomyModerationDismissed),
            (ReviewDisputeAction.noAction.titleKey, .nativeTaxonomyModerationNoAction),
            (ReviewDisputeAction.remove.titleKey, .nativeTaxonomyModerationRemove),
            (ReviewDisputeAction.annotate.titleKey, .nativeTaxonomyModerationAnnotate),
            (ReviewDisputeAction.dismiss.titleKey, .nativeTaxonomyModerationDismissed),
            (
                AdminReviewQueueClearanceStatus.rejected.titleKey,
                .nativeTaxonomyModerationRejected
            ),
            (
                AdminReviewQueueClearanceStatus.inReview.titleKey,
                .nativeTaxonomyModerationInReview
            ),
            (
                AdminReviewQueueClearanceStatus.approved.titleKey,
                .nativeTaxonomyModerationApproved
            ),
            (
                AdminReviewQueueClearanceStatus.pending.titleKey,
                .nativeTaxonomyModerationPending
            )
        ])
    }

    func testPresentationEnumsMapEveryCaseToTypedKeys() {
        assertMappings([
            (ListsItemFilter.all.titleKey, .nativeSwiftCommonAll),
            (ListsItemFilter.reading.titleKey, .nativeSwiftPresentationValuesReading),
            (ListsItemFilter.watch.titleKey, .nativeSwiftPresentationValuesWatch),
            (ListsItemFilter.listen.titleKey, .nativeSwiftPresentationValuesListen),
            (ListItemType.rssFeedItem.titleKey, .nativeSwiftPresentationValuesRssFeedItem),
            (ListItemType.post.titleKey, .nativeSwiftPresentationValuesPost),
            (CommunityMemberRole.owner.titleKey, .nativeSwiftPresentationValuesOwner),
            (CommunityMemberRole.moderator.titleKey, .nativeSwiftPresentationValuesModerator),
            (CommunityMemberRole.member.titleKey, .nativeSwiftPresentationValuesMember),
            (ProfileLinkType.url.titleKey, .nativeSwiftSettingsUrl),
            (ProfileLinkType.twitter.titleKey, .nativeSwiftPresentationValuesTwitter),
            (ProfileLinkType.facebook.titleKey, .nativeSwiftPresentationValuesFacebook),
            (ProfileLinkType.instagram.titleKey, .nativeSwiftPresentationValuesInstagram),
            (ProfileLinkType.github.titleKey, .nativeSwiftPresentationValuesGithub),
            (ProfileLinkType.linkedin.titleKey, .nativeSwiftPresentationValuesLinkedin),
            (ProfileLinkType.youtube.titleKey, .nativeSwiftPresentationValuesYoutube),
            (ProfileLinkType.tiktok.titleKey, .nativeSwiftPresentationValuesTiktok),
            (UserPrivacyAudience.everyone.titleKey, .nativeSwiftSettingsEveryone),
            (UserPrivacyAudience.users.titleKey, .nativeSwiftSettingsUsers),
            (UserPrivacyAudience.followers.titleKey, .nativeSwiftSettingsFollowers),
            (UserPrivacyAudience.mutualFollowers.titleKey, .nativeSwiftSettingsMutualFollowers),
            (UserPrivacyAudience.nobody.titleKey, .nativeSwiftPresentationValuesNobody)
        ])
    }

    func testIdentityAndPostEnumsMapEveryCaseToTypedKeys() {
        assertMappings([
            (DisplayNameSource.username.titleKey, .nativeSwiftPresentationValuesUsername),
            (DisplayNameSource.facebook.titleKey, .nativeSwiftPresentationValuesFacebook),
            (DisplayNameSource.xTwitter.titleKey, .nativeSwiftPresentationValuesX),
            (DisplayNameSource.apple.titleKey, .nativeSwiftPresentationValuesApple),
            (DisplayNameSource.google.titleKey, .nativeSwiftPresentationValuesGoogle),
            (DisplayNameSource.linkedin.titleKey, .nativeSwiftPresentationValuesLinkedin),
            (DisplayNameSource.microsoft.titleKey, .nativeSwiftPresentationValuesMicrosoft),
            (DisplayNameSource.github.titleKey, .nativeSwiftPresentationValuesGithub),
            (PostType.discussion.titleKey, .nativeSwiftPresentationValuesDiscussion),
            (PostType.review.titleKey, .nativeSwiftPresentationValuesReview),
            (PostType.dataPoint.titleKey, .nativeSwiftPresentationValuesDataPoint),
            (PostType.comment.titleKey, .nativeSwiftDesignSystemComment),
            (PostType.article.titleKey, .nativeSwiftRouteSurfaceArticle),
            (PostType.blogPost.titleKey, .nativeSwiftPresentationValuesBlogPost),
            (PostType.story.titleKey, .nativeSwiftRouteSurfaceStory),
            (PostType.link.titleKey, .nativeSwiftRouteSurfaceLink),
            (
                PostType.topicRecommendation.titleKey,
                .nativeSwiftPresentationValuesTopicRecommendation
            )
        ])
    }

    func testPresentationHelpersLocalizeKnownValuesAndPreserveUnknownValues() {
        XCTAssertEqual(
            [
                listItemMediaTypeText(nil),
                listItemMediaTypeText("article"),
                listItemMediaTypeText("audio"),
                listItemMediaTypeText("video"),
                listItemMediaTypeText("document")
            ],
            [
                .message(.nativeSwiftPresentationValuesItem),
                .message(.nativeSwiftRouteSurfaceArticle),
                .message(.nativeSwiftPresentationValuesAudio),
                .message(.nativeSwiftPresentationValuesVideo),
                .verbatim("document")
            ]
        )
        XCTAssertEqual(
            ["owner", "moderator", "member", "guest"].map(communityMemberRoleText),
            [
                .message(.nativeSwiftPresentationValuesOwner),
                .message(.nativeSwiftPresentationValuesModerator),
                .message(.nativeSwiftPresentationValuesMember),
                .verbatim("guest")
            ]
        )
        XCTAssertEqual(
            ["free", "plus", "pro", "enterprise"].map(membershipPlanText),
            [
                .message(.nativeSwiftMembershipFree),
                .message(.nativeSwiftPresentationValuesPlus),
                .message(.nativeSwiftPresentationValuesPro),
                .verbatim("enterprise")
            ]
        )
    }

    func testPostAndVisibilityHelpersPreserveRoutingAndProtocolValues() {
        XCTAssertEqual(PostType.discussion.protocolValue, "discussion")
        XCTAssertEqual(PostType.dataPoint.routeSegment, "data-point")
        XCTAssertEqual(PostType.blogPost.routeSegment, "blog-post")
        XCTAssertEqual(PostType.topicRecommendation.routeSegment, "topic-recommendation")
        XCTAssertEqual(PostType.article.routeSegment, "article")
        XCTAssertEqual(postTypeText(.review), .message(.nativeSwiftPresentationValuesReview))
        XCTAssertEqual(postTypeText("story"), .message(.nativeSwiftRouteSurfaceStory))
        XCTAssertEqual(postTypeText("future_type"), .verbatim("future_type"))
        XCTAssertEqual(listVisibilityText(.unlisted), .message(.nativeTaxonomyListsUnlisted))
    }

    private func assertMappings(
        _ mappings: [(actual: UiMessageKey, expected: UiMessageKey)],
        file: StaticString = #filePath,
        line: UInt = #line
    ) {
        for mapping in mappings {
            XCTAssertEqual(mapping.actual, mapping.expected, file: file, line: line)
        }
    }
}
