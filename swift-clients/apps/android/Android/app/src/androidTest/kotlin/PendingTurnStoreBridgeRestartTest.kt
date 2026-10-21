package voucha.android

import android.content.pm.PackageManager
import android.net.Uri
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.nio.charset.StandardCharsets
import java.util.Base64
import java.util.Locale
import org.json.JSONArray
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test
import org.junit.runner.RunWith

/** The host force-stops the target app between these separate instrumentation launches. */
@RunWith(AndroidJUnit4::class)
class PendingTurnStoreBridgeRestartTest {
    private val ownerA: String get() = ownerKey("synthetic-owner-a")
    private val ownerB: String get() = ownerKey("synthetic-owner-b")

    @Test fun seedCheckedOwnerRecords() {
        PendingTurnStoreBridge.save(ownerA, conversationA, bodyA)
        PendingTurnStoreBridge.save(ownerB, conversationB, bodyB)
        assertRecord(ownerA, conversationA, bodyA)
        assertRecord(ownerB, conversationB, bodyB)

        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val records = context.noBackupFilesDir.listFiles().orEmpty().filter {
            it.name.startsWith("voucha-pending-v1-") && it.name.endsWith(".bin")
        }
        assertEquals(2, records.size)
        for (record in records) {
            val ciphertext = String(record.readBytes(), StandardCharsets.UTF_8)
            assertFalse(ciphertext.contains(bodyA))
            assertFalse(ciphertext.contains(bodyB))
            assertFalse(ciphertext.contains("synthetic assistant response"))
        }
    }

    @Test fun readExactRecordsAfterRestart() {
        assertRecord(ownerA, conversationA, bodyA)
        assertRecord(ownerB, conversationB, bodyB)
        // A repeated checked save of the same bytes is safe after a lost acknowledgement.
        PendingTurnStoreBridge.save(ownerA, conversationA, bodyA)
        assertRecord(ownerA, conversationA, bodyA)
    }

    @Test fun deleteOnlyMatchingOwnerRecord() {
        try {
            PendingTurnStoreBridge.delete(ownerA, conversationA, bodyB)
            throw AssertionError("Mismatched pending body was deleted")
        } catch (_: IllegalStateException) {
            assertRecord(ownerA, conversationA, bodyA)
        }
        PendingTurnStoreBridge.delete(ownerA, conversationA, bodyA)
        assertEmpty(ownerA)
        assertRecord(ownerB, conversationB, bodyB)
    }

    @Test fun readTargetedDeletionAfterRestart() {
        assertEmpty(ownerA)
        // A directory sync failure can arrive after the file was removed; retry remains safe.
        PendingTurnStoreBridge.delete(ownerA, conversationA, bodyA)
        assertEmpty(ownerA)
        assertRecord(ownerB, conversationB, bodyB)
    }

    private fun ownerKey(userID: String): String {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        val metadata = context.packageManager.getApplicationInfo(
            context.packageName,
            PackageManager.GET_META_DATA,
        ).metaData
        val baseURL = Uri.parse(requireNotNull(metadata.getString("ai.voucha.VOUCHA_API_BASE_URL")))
        val scheme = requireNotNull(baseURL.scheme).lowercase(Locale.ROOT)
        require(scheme == "https" || scheme == "http")
        val host = requireNotNull(baseURL.host).lowercase(Locale.ROOT)
        val port = if (baseURL.port >= 0) baseURL.port else if (scheme == "https") 443 else 80
        require(port in 1..65535)
        val origin = "voucha.session.cookies.$scheme://$host:$port"
        val path = baseURL.encodedPath.orEmpty()
        val basePath = if (path == "/") "" else path.removeSuffix("/")
        return "voucha.pending.turn.v1.${part(origin)}:${part(basePath)}:${part(userID)}"
    }

    private fun part(value: String): String = "${value.toByteArray(StandardCharsets.UTF_8).size}:$value"

    private fun assertRecord(ownerID: String, conversationID: String, body: String) {
        val records = JSONArray(PendingTurnStoreBridge.load(ownerID))
        assertEquals(1, records.length())
        val record = records.getJSONObject(0)
        assertEquals(conversationID, record.getString("conversationID"))
        assertEquals(body, record.getString("body"))
        assertEquals(syntheticJSON(body), String(Base64.getDecoder().decode(body), StandardCharsets.UTF_8))
    }

    private fun assertEmpty(ownerID: String) {
        assertEquals(0, JSONArray(PendingTurnStoreBridge.load(ownerID)).length())
    }

    private fun syntheticJSON(body: String): String = if (body == bodyA) jsonA else jsonB

    companion object {
        private const val conversationA = "synthetic-conversation-a"
        private const val conversationB = "synthetic-conversation-b"
        private const val jsonA = """{"message":"synthetic request A","user_message_id":"synthetic-user-a",""" +
            """"assistant_message_id":"synthetic-assistant-a","assistant_content":"synthetic assistant response A",""" +
            """"model_provider":"android_aicore","model_name":"android-aicore-system"}"""
        private const val jsonB = """{"message":"synthetic request B","user_message_id":"synthetic-user-b",""" +
            """"assistant_message_id":"synthetic-assistant-b","assistant_content":"synthetic assistant response B",""" +
            """"model_provider":"android_aicore","model_name":"android-aicore-system"}"""
        private val bodyA = Base64.getEncoder().encodeToString(jsonA.toByteArray(StandardCharsets.UTF_8))
        private val bodyB = Base64.getEncoder().encodeToString(jsonB.toByteArray(StandardCharsets.UTF_8))
    }
}
