import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import test from "node:test";

const repositoryRoot = resolve(import.meta.dirname, "..");
const prePushPath = join(
  repositoryRoot,
  "swift-clients/apps/android/tooling/pre-push.sh",
);

test("builds only the Android app Gradle task closure", async () => {
  const prePush = await readFile(prePushPath, "utf8");
  const qualifiedAssemble = prePush.indexOf("./gradlew :app:assembleDebug");
  const unqualifiedAssemble = prePush.indexOf("./gradlew assembleDebug");
  const skipAndroidTest = prePush.indexOf("skip android test \\");

  assert.ok(qualifiedAssemble >= 0, "pre-push must qualify the Android app task");
  assert.equal(unqualifiedAssemble, -1, "pre-push must not assemble every Gradle subproject");
  assert.ok(
    skipAndroidTest > qualifiedAssemble,
    "Skip Android tests must run after the app assembly",
  );
});
