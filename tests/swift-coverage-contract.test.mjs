import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { chmod, mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, join, relative, resolve } from "node:path";
import { promisify } from "node:util";
import test from "node:test";

const execFileAsync = promisify(execFile);
const repositoryRoot = resolve(import.meta.dirname, "..");
const writeLcov = join(repositoryRoot, "swift-clients/tooling/write-lcov.sh");
const swiftCoverage = join(repositoryRoot, "scripts/swift-coverage.mjs");

async function writeExecutable(path, contents) {
  await writeFile(path, contents);
  await chmod(path, 0o755);
}

async function lcovFixture(t) {
  const fixtureRoot = await mkdtemp(join(repositoryRoot, ".write-lcov-test-"));
  const binDirectory = await mkdtemp(join(tmpdir(), "voucha-xcrun-"));
  const packageDirectory = join(fixtureRoot, "package");
  const buildDirectory = join(packageDirectory, ".build/debug");
  const bundleName = "VouchaTests";
  const binary = join(buildDirectory, `${bundleName}.xctest/Contents/MacOS/${bundleName}`);
  const argumentsPath = join(fixtureRoot, "xcrun-arguments.txt");
  const lcovPath = join(fixtureRoot, "llvm-cov.lcov");
  await mkdir(dirname(binary), { recursive: true });
  await writeFile(join(buildDirectory, "default.profdata"), "profile");
  await writeExecutable(binary, "#!/usr/bin/env bash\nexit 0\n");
  await writeFile(
    lcovPath,
    `SF:${repositoryRoot}/swift-clients/core/Sources/VouchaCore/Example.swift\nDA:1,1\nend_of_record\n`,
  );
  await writeExecutable(
    join(binDirectory, "xcrun"),
    '#!/usr/bin/env bash\nprintf \'%s\\n\' "$@" > "$XCRUN_ARGUMENTS_PATH"\ncat "$LCOV_FIXTURE_PATH"\n',
  );
  t.after(() =>
    Promise.all([
      rm(fixtureRoot, { recursive: true, force: true }),
      rm(binDirectory, { recursive: true, force: true }),
    ]),
  );
  return {
    argumentsPath,
    environment: {
      ...process.env,
      LCOV_FIXTURE_PATH: lcovPath,
      PATH: `${binDirectory}:${process.env.PATH}`,
      XCRUN_ARGUMENTS_PATH: argumentsPath,
    },
    fixtureRoot,
    packagePath: relative(repositoryRoot, packageDirectory),
    bundleName,
  };
}

test("writes normalized LCOV with the intended .build ignore regex", async (t) => {
  const fixture = await lcovFixture(t);
  const outputPath = join(fixture.fixtureRoot, "coverage/lcov.info");
  await execFileAsync("bash", [writeLcov, fixture.packagePath, fixture.bundleName, outputPath], {
    cwd: repositoryRoot,
    env: fixture.environment,
  });
  assert.equal(
    await readFile(outputPath, "utf8"),
    "SF:swift-clients/core/Sources/VouchaCore/Example.swift\nDA:1,1\nend_of_record\n",
  );
  assert.deepEqual((await readFile(fixture.argumentsPath, "utf8")).trim().split("\n"), [
    "llvm-cov",
    "export",
    "-format=lcov",
    join(
      repositoryRoot,
      fixture.packagePath,
      ".build/debug/VouchaTests.xctest/Contents/MacOS/VouchaTests",
    ),
    "-instr-profile",
    join(repositoryRoot, fixture.packagePath, ".build/debug/default.profdata"),
    "-ignore-filename-regex=\\.build",
  ]);
});

test("fails LCOV export when profile discovery is ambiguous", async (t) => {
  const fixture = await lcovFixture(t);
  await mkdir(join(fixture.fixtureRoot, "package/.build/release"), { recursive: true });
  await writeFile(join(fixture.fixtureRoot, "package/.build/release/default.profdata"), "profile");
  await assert.rejects(
    execFileAsync(
      "bash",
      [writeLcov, fixture.packagePath, fixture.bundleName, "coverage/lcov.info"],
      {
        cwd: repositoryRoot,
        env: fixture.environment,
      },
    ),
    /Expected exactly one default\.profdata profile, found 2\./,
  );
});

test("requires a full pull-request base SHA before invoking coverage-check", async () => {
  await assert.rejects(
    execFileAsync(process.execPath, [swiftCoverage], {
      cwd: repositoryRoot,
      env: { ...process.env, BASE_SHA: "not-a-sha" },
    }),
    /BASE_SHA must be the 40-character pull-request base SHA/,
  );
});

test("invokes coverage-check with both Swift LCOV artifacts", async (t) => {
  const binDirectory = await mkdtemp(join(tmpdir(), "voucha-pnpm-"));
  const argumentsPath = join(binDirectory, "pnpm-arguments.txt");
  await writeExecutable(
    join(binDirectory, "pnpm"),
    '#!/usr/bin/env bash\nprintf \'%s\\n\' "$@" > "$PNPM_ARGUMENTS_PATH"\n',
  );
  t.after(() => rm(binDirectory, { recursive: true, force: true }));
  await execFileAsync(process.execPath, [swiftCoverage], {
    cwd: repositoryRoot,
    env: {
      ...process.env,
      BASE_SHA: "a".repeat(40),
      PATH: `${binDirectory}:${process.env.PATH}`,
      PNPM_ARGUMENTS_PATH: argumentsPath,
    },
  });
  assert.deepEqual((await readFile(argumentsPath, "utf8")).trim().split("\n"), [
    "exec",
    "coverage-check",
    "check",
    "--rules",
    ".coverage-rules.yml",
    "--artifacts",
    "coverage",
    "--require-artifact",
    "core/lcov.info",
    "--require-artifact",
    "ui/lcov.info",
    "--base",
    "a".repeat(40),
    "--head",
    "HEAD",
    "--aggregate-artifacts",
    "--fail-on-empty",
    "--annotate-source",
  ]);
});
