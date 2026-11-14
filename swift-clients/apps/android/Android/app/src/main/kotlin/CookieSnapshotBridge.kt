package voucha.android

import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import java.io.IOException

/** Uses the pinned SkipKeychain 0.3.2 file and encryption schemes without its global reset path. */
object CookieSnapshotBridge {
    private const val fileName = "tools.skip.SkipKeychain"
    private var cachedPreferences: SharedPreferences? = null

    @Synchronized
    fun stringValue(key: String): String? = preferences().getString(key, null)

    @Synchronized
    fun setValue(key: String, value: String) {
        if (!preferences().edit().putString(key, value).commit()) {
            throw IOException("Cookie snapshot write was not persisted")
        }
    }

    @Synchronized
    fun removeValue(key: String) {
        if (!preferences().edit().remove(key).commit()) {
            throw IOException("Cookie snapshot removal was not persisted")
        }
    }

    private fun preferences(): SharedPreferences {
        cachedPreferences?.let { return it }
        val alias = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
        // A corrupt or unavailable key must remain visible to the existing retry/discard flow.
        // Do not delete this shared file: it also holds unrelated endpoint secrets.
        return EncryptedSharedPreferences.create(
            fileName,
            alias,
            AndroidAppMain.cookieSnapshotContext,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        ).also { cachedPreferences = it }
    }
}
