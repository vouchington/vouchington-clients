package voucha.android

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.system.Os
import android.system.OsConstants
import java.io.File
import java.io.FileOutputStream
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import java.security.KeyStore
import java.security.MessageDigest
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import org.json.JSONArray
import org.json.JSONObject

/** Blocking, owner-scoped persistence boundary. Swift calls it from a detached task. */
object PendingTurnStoreBridge {
    private const val prefix = "voucha-pending-v1-"
    private val magic = byteArrayOf(0x56, 0x50, 0x54, 0x31)

    @Synchronized
    fun load(ownerID: String): String {
        requireNoDiscardInProgress(ownerID)
        val result = JSONArray()
        val ownerPrefix = "$prefix${digest(ownerID)}-"
        for (file in directory().listFiles() ?: throw IllegalStateException("Pending turn directory unavailable")) {
            if (!file.name.startsWith(ownerPrefix) || !file.name.endsWith(".bin")) continue
            val entry = read(file, ownerID)
            if (entry.ownerID != ownerID || file.name != filename(entry.ownerID, entry.conversationID)) {
                throw IllegalStateException("Pending turn owner or path mismatch")
            }
            result.put(JSONObject().put("conversationID", entry.conversationID).put("body", entry.body))
        }
        return result.toString()
    }

    @Synchronized
    fun save(ownerID: String, conversationID: String, body: String) {
        require(ownerID.isNotEmpty() && conversationID.isNotEmpty() && body.isNotEmpty())
        requireNoDiscardInProgress(ownerID)
        Base64.getDecoder().decode(body)
        val dir = directory()
        val file = File(dir, filename(ownerID, conversationID))
        if (file.exists()) {
            val existing = read(file, ownerID)
            if (existing != Entry(ownerID, conversationID, body)) {
                throw IllegalStateException("Different pending turn already occupies conversation")
            }
            val fd = Os.open(file.path, OsConstants.O_RDONLY, 0)
            try { Os.fsync(fd) } finally { Os.close(fd) }
            syncDirectory(dir)
            return
        }
        val key = key(ownerID, create = true)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, key)
        cipher.updateAAD(file.name.toByteArray(StandardCharsets.UTF_8))
        val plaintext = JSONObject()
            .put("ownerID", ownerID)
            .put("conversationID", conversationID)
            .put("body", body)
            .toString().toByteArray(StandardCharsets.UTF_8)
        val encrypted = cipher.doFinal(plaintext)
        val iv = cipher.iv
        require(iv.size == 12)
        val bytes = magic + iv + encrypted
        val temp = File.createTempFile("$prefix${digest(ownerID)}-write-", ".tmp", dir)
        try {
            FileOutputStream(temp).use { output ->
                output.write(bytes)
                output.fd.sync()
            }
            Os.rename(temp.path, file.path)
            syncDirectory(dir)
        } finally {
            if (temp.exists()) Files.deleteIfExists(temp.toPath())
        }
    }

    @Synchronized
    fun delete(ownerID: String, conversationID: String, body: String) {
        requireNoDiscardInProgress(ownerID)
        val dir = directory()
        val file = File(dir, filename(ownerID, conversationID))
        if (read(file, ownerID) != Entry(ownerID, conversationID, body)) {
            throw IllegalStateException("Pending turn changed before deletion")
        }
        Files.delete(file.toPath())
        syncDirectory(dir)
    }

    /** Called only after an owner-bound confirmation covering every pending turn on this device. */
    @Synchronized
    fun discardOwner(ownerID: String) {
        require(ownerID.isNotEmpty())
        val dir = directory()
        val marker = discardMarker(ownerID)
        if (!marker.exists()) Files.createFile(marker.toPath())
        val markerFD = Os.open(marker.path, OsConstants.O_RDONLY, 0)
        try { Os.fsync(markerFD) } finally { Os.close(markerFD) }
        syncDirectory(dir)
        val ownerPrefix = "$prefix${digest(ownerID)}-"
        val files = dir.listFiles() ?: throw IllegalStateException("Pending turn directory unavailable")
        for (file in files) {
            if (file.name.startsWith(ownerPrefix) &&
                (file.name.endsWith(".bin") || file.name.endsWith(".tmp"))) {
                Files.delete(file.toPath())
            }
        }
        syncDirectory(dir)
        val store = KeyStore.getInstance("AndroidKeyStore")
        store.load(null)
        if (store.containsAlias(alias(ownerID))) store.deleteEntry(alias(ownerID))
        Files.delete(marker.toPath())
        syncDirectory(dir)
    }

    private data class Entry(val ownerID: String, val conversationID: String, val body: String)

    private fun read(file: File, ownerID: String): Entry {
        val bytes = Files.readAllBytes(file.toPath())
        if (bytes.size <= magic.size + 12 + 16 || !bytes.take(magic.size).toByteArray().contentEquals(magic)) {
            throw IllegalStateException("Pending turn format invalid")
        }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, key(ownerID, create = false), GCMParameterSpec(128, bytes.copyOfRange(4, 16)))
        cipher.updateAAD(file.name.toByteArray(StandardCharsets.UTF_8))
        val decoded = JSONObject(String(cipher.doFinal(bytes.copyOfRange(16, bytes.size)), StandardCharsets.UTF_8))
        return Entry(decoded.getString("ownerID"), decoded.getString("conversationID"), decoded.getString("body"))
    }

    private fun key(ownerID: String, create: Boolean): SecretKey {
        val store = KeyStore.getInstance("AndroidKeyStore")
        store.load(null)
        val alias = alias(ownerID)
        val existing = store.getKey(alias, null)
        if (existing is SecretKey) return existing
        if (!create || (directory().listFiles() ?: throw IllegalStateException("Pending turn directory unavailable"))
                .any { it.name.startsWith("$prefix${digest(ownerID)}-") && it.name.endsWith(".bin") }) {
            throw IllegalStateException("Pending turn encryption key unavailable")
        }
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
        generator.init(
            KeyGenParameterSpec.Builder(alias, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build()
        )
        return generator.generateKey()
    }

    private fun directory(): File = AndroidAppMain.pendingTurnContext.noBackupFilesDir

    private fun filename(ownerID: String, conversationID: String): String =
        "$prefix${digest(ownerID)}-${digest(conversationID)}.bin"

    private fun alias(ownerID: String): String = "$prefix${digest(ownerID)}-key"

    private fun discardMarker(ownerID: String): File = File(directory(), "$prefix${digest(ownerID)}-discarding")

    private fun requireNoDiscardInProgress(ownerID: String) {
        if (discardMarker(ownerID).exists()) throw IllegalStateException("Pending turn discard incomplete")
    }

    private fun digest(value: String): String = MessageDigest.getInstance("SHA-256")
        .digest(value.toByteArray(StandardCharsets.UTF_8))
        .joinToString("") { "%02x".format(it) }

    private fun syncDirectory(directory: File) {
        val fd = Os.open(directory.path, OsConstants.O_RDONLY, 0)
        try { Os.fsync(fd) } finally { Os.close(fd) }
    }
}
