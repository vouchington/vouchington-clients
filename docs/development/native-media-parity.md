# Native media parity audit

This inventory records the supported media surfaces audited for
[clients #400](https://github.com/vouchington/vouchington-clients/issues/400). It is scoped to the
producer contract selected by [contracts/vouchington.json](../../contracts/vouchington.json), obtained
with `pnpm run contracts:pull`. It does not introduce another contract source or pin.

## Prior delivery

Placement consumers already landed for communities (#285), public topics (#290), friends and
profiles (#293), post/comment authors (#294), community members (#295), direct-message participants
(#297), followed topics (#298), follower-distribution recipients (#305), and moderator statistics
(#307). Profile links and avatar previews followed in #416. The later #455 slice fixed topic-logo
preview fallback visibility; #466 and #472 added absent-placement rendering evidence for members
and moderator statistics. The #406 profile-preview lifecycle tests and #284 Windows decoder tests
are separate kinds of evidence.

## Placement consumers

The following names identify test files in the owning client's UI/App test directory. Fixture names
are relative to the pulled contract's `api-fixtures/v1/responses` directory.

| Surface                                  | Representative producer fixture                                                         | Swift evidence                                                          | .NET evidence                                                       |
| ---------------------------------------- | --------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Community images and members             | `web.communities.show.default.json`, `web.communities.members.default.json`             | `CommunityPlacementSurfaceTests`, `CommunityMemberAvatarRenderingTests` | `CommunityPlacementRenderingTests`                                  |
| Community moderator statistics           | `web.communities.members.default.json`                                                  | `CommunityModeratorStatsAvatarTests`                                    | `CommunityModeratorStatsAvatarRenderingTests`                       |
| Direct-message participants              | `native.messages.participants.default.json`                                             | `DirectMessagesParticipantAvatarTests`                                  | `DirectMessageParticipantRenderingTests`                            |
| Friends and recommendations              | `native.users.followers.search.json`, `native.friend-recommendations.default.json`      | `FriendsAvatarPlacementTests`, `FriendRecommendationsViewTests`         | `FriendAvatarPlacementRenderingTests`                               |
| Post/comment authors                     | `native.comments.descendants.default.json`                                              | `PostAuthorAvatarRenderingTests`                                        | `PostAuthorAvatarRenderingTests`                                    |
| Topic lists, details and followed topics | `web.topics.show.default.json`, `native.users.profile.topics-following.first-page.json` | `PublicTopicImageRenderingTests`, profile collection surface tests      | `PublicTopicImageRenderingTests`, `ProfileTopicImageRenderingTests` |
| Profile links                            | `native.users.profile.default.json`                                                     | `NativeProfileLinkPlacementTests`                                       | `FriendAvatarPlacementRenderingTests`                               |
| Follower-distribution recipients         | `swift.users.followers.default.json`                                                    | `FollowerDistributionActionsTests`                                      | `FollowerDistributionRecipientAvatarTests`                          |
| Sensitive review-queue images            | `native.moderation.review-queue.default.json`                                           | `NativeReviewQueueMediaSurfaceTests`                                    | `ReviewQueuePageTests`                                              |

The review-queue fixture supplies direct `image_id`, `placement_id`, and `placement_revision`
fields under `media_reveal.images`, rather than a nested `*_placement` object. Its reveal gate must
remain effective before any placement image is mounted. Revealed images must use the current
client's configured image host, including a custom image host.

Missing and withheld placements use the same null/absent descriptor shape at this contract. A
legacy image ID can still be present; it is not permission to construct a display URL. Anonymous
and deleted author/member cases retain their existing identity and fallback behavior. Those states
are not invented for unrelated topic or community image descriptors.

`ModerationActorSummary` has only a legacy profile image ID at this contract and therefore has no
placement-backed avatar to render. Copyright staff-queue image placements are decoded by Core, but
have no current app-level image consumer; they are not an additional rendered row in this inventory.
No localization addition was needed for the audited changes. One positive producer-fixture case is
still missing: `web.communities.moderator-stats.default.json` has
`users["user-1"].profile_image_id: null` and omits that user's `profile_image_placement`. The positive
moderator-avatar tests currently copy the descriptor from the community-member fixture. The
producer should add a moderator-statistics response fixture with a non-null user profile image ID
and its matching `{ image_id, placement_id, placement_revision }` descriptor. Existing absent-user
and withheld-placement cases should remain. Local enrichment proves native rendering, but does not
prove that positive descriptor is exported by the moderator-statistics endpoint.

## Selected-byte previews

| Surface               | Swift evidence                                                        | .NET evidence                                                          |
| --------------------- | --------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| Profile avatar        | `ProfileViewModelImageUploadTests`, shared `LocalImagePreviewTests`   | `ProfilePageLocalAvatarPreviewTests`, `AvatarPreviewCancellationTests` |
| Topic logo/hero field | `NativeTopicLogoLocalPreviewTests`, `NativeTopicHeldImageUploadTests` | `TopicLogoLocalPreviewTests`                                           |
| Post compose          | `NativePostComposePendingImagePreviewTests`                           | `PostComposeLocalPreviewPageTests`                                     |

Selected bytes and bounded thumbnails belong to the owning UI session. Replacement, cancellation,
navigation and session teardown must clear them and prevent late completion from restoring them.
Unavailable or malformed bytes retain the existing localized, accessible fallback. A successful
upload's returned ready image ID is a mutation input; persisted rendering uses the returned
placement descriptor, not that ID or the upload URL.

## Evidence limits

Portable .NET App tests inspect real control properties and preview streams, with a selected-byte
decoder seam where the portable target has no native decoder. They do not establish native bitmap
decoding or visible device pixels. `WindowsLocalImagePreviewDecoderTests` exercise the production
Windows decoder separately and must run on the supported Windows runner. Native bitmap/visible
thumbnail and device acceptance must remain explicit when reporting #400; passing portable tests
alone does not complete that acceptance.

Local verification results, unavailable platform/toolchain checks and hosted build outcomes belong
in the implementing PR's verification record. Do not infer all-surface acceptance from one earlier
merged slice or from the closure of producer #805.
