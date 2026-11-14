package voucha.android

import android.content.pm.PackageManager
import android.net.Uri
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.junit.runner.RunWith

/** Each method is a separate instrumentation launch; the host force-stops the app between them. */
@RunWith(AndroidJUnit4::class)
class CookieSnapshotBridgeRestartTest {
    private val cookieKey: String
        get() {
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
            return "voucha.session.cookies.$scheme://$host:$port"
        }

    @Test fun seedCheckedEntries() {
        CookieSnapshotBridge.setValue(cookieKey, syntheticCookie)
        CookieSnapshotBridge.setValue(otherEntryKey, syntheticOtherEntry)
        assertEquals(syntheticCookie, CookieSnapshotBridge.stringValue(cookieKey))
        assertEquals(syntheticOtherEntry, CookieSnapshotBridge.stringValue(otherEntryKey))
    }

    @Test fun readEntriesAfterRestart() {
        assertEquals(syntheticCookie, CookieSnapshotBridge.stringValue(cookieKey))
        assertEquals(syntheticOtherEntry, CookieSnapshotBridge.stringValue(otherEntryKey))
    }

    @Test fun removeOnlyCookieWithCheckedCommit() {
        CookieSnapshotBridge.removeValue(cookieKey)
        assertNull(CookieSnapshotBridge.stringValue(cookieKey))
        assertEquals(syntheticOtherEntry, CookieSnapshotBridge.stringValue(otherEntryKey))
    }

    @Test fun readRemovalAfterRestart() {
        assertNull(CookieSnapshotBridge.stringValue(cookieKey))
        assertEquals(syntheticOtherEntry, CookieSnapshotBridge.stringValue(otherEntryKey))
    }

    companion object {
        // Deliberately synthetic: no account, auth cookie, backend call, or endpoint credential.
        private const val syntheticCookie = "cookie-restart-proof-v1"
        private const val otherEntryKey = "voucha.test.unrelated-endpoint-entry"
        private const val syntheticOtherEntry = "unrelated-entry-must-survive-v1"
    }
}
