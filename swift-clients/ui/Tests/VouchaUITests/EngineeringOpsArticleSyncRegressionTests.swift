@testable import VouchaFeatures
import XCTest

@MainActor
final class EngineeringOpsArticleSyncRegressionTests: NativeRouteSurfaceViewModelTestCase {
    func testPostgresqlViewModelTriggerArticleSyncIgnoresStatusRefreshFailure() async throws {
        stubPostgresqlLoad()
        CannedFeedURLProtocol.handlers["/api/v1/article-syncs"] = (Data(#"{"jobId":"job-3"}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/article-syncs/job-3"] = (
            Data(#"{"error":"offline"}"#.utf8),
            500
        )
        let viewModel = try NativeEngineeringPostgresqlViewModel(client: makeClient())

        await viewModel.load()
        await viewModel.triggerArticleSync()

        XCTAssertEqual(viewModel.articleSyncJobId, "job-3")
        XCTAssertNil(viewModel.articleSyncStatus)
        XCTAssertNil(viewModel.actionErrorMessage)
    }

    private func stubPostgresqlLoad() {
        CannedFeedURLProtocol.handlers["/api/v1/psql/migrations"] = (
            Data(#"{"applied":["001.sql"],"pending":["002.sql"],"total":2}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/psql/partitions"] = (
            Data(
                """
                {
                  "tables": [
                    {
                      "name": "posts",
                      "partition_count": 1,
                      "total_size_bytes": 123,
                      "partitions": [
                        { "name": "posts_2026_07", "size_bytes": 123 }
                      ]
                    }
                  ]
                }
                """
                .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/psql/jobs"] = (Data(#"{"success":true}"#.utf8), 200)
    }
}
