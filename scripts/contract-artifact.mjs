import { createHash } from "node:crypto";
import { copyFile, lstat, mkdir, readFile, readdir, writeFile } from "node:fs/promises";
import { isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = resolve(import.meta.dirname, "..");
const configPath = join(repositoryRoot, "contracts/filaments.json");
const shaPattern = /^[0-9a-f]{40}$/i;
const repositoryPattern = /^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/;

function fail(message) {
  throw new Error(`Contract artifact failed: ${message}`);
}

function requiredString(value, label) {
  if (typeof value !== "string" || value.trim() === "" || value.includes("\0"))
    fail(`${label} is required`);
  return value.trim();
}

function sha(value, label) {
  const result = requiredString(value, label);
  if (!shaPattern.test(result)) fail(`${label} must be a 40-character Git SHA`);
  return result.toLowerCase();
}

function positiveInteger(value, label) {
  const text = requiredString(String(value), label);
  if (!/^[1-9][0-9]*$/.test(text) || !Number.isSafeInteger(Number(text)))
    fail(`${label} must be a positive safe integer`);
  return text;
}

function absolutePath(value, label) {
  const result = requiredString(value, label);
  if (!isAbsolute(result)) fail(`${label} must be an absolute path`);
  return resolve(result);
}

function artifactPath(value) {
  if (typeof value !== "string" || value.length === 0 || value.includes("\0"))
    fail("invalid artifact path");
  if (
    value
      .split("/")
      .some((component) => component === "" || component === "." || component === "..")
  )
    fail(`invalid artifact path: ${value}`);
  return value;
}

async function info(path, label) {
  let result;
  try {
    result = await lstat(path);
  } catch (error) {
    if (error.code === "ENOENT") return undefined;
    fail(`cannot inspect ${label}: ${error.message}`);
  }
  if (result.isSymbolicLink()) fail(`symbolic link found at ${label}`);
  return result;
}

async function realDirectory(path, label) {
  const result = await info(path, label);
  if (!result) fail(`missing directory at ${label}`);
  if (!result.isDirectory()) fail(`expected directory at ${label}`);
}

async function descendantDirectory(root, child, label) {
  await realDirectory(root, `${label} root`);
  let current = root;
  for (const component of child.split("/")) {
    current = join(current, component);
    await realDirectory(current, `${label}/${component}`);
  }
  return current;
}

async function configuredPaths() {
  let config;
  try {
    config = JSON.parse(await readFile(configPath, "utf8"));
  } catch (error) {
    fail(`cannot read ${configPath}: ${error.message}`);
  }
  exactKeys(config, ["schemaVersion", "repository", "ref", "paths"], "contract configuration");
  if (config.schemaVersion !== 1) fail("unsupported contract configuration schema");
  if (!repositoryPattern.test(requiredString(config.repository, "contract repository")))
    fail("contract repository must be owner/name");
  requiredString(config.ref, "contract ref");
  if (!Array.isArray(config.paths) || config.paths.length === 0)
    fail("contract paths must be a nonempty array");
  const seen = new Set();
  for (const path of config.paths) {
    artifactPath(path);
    if (
      seen.has(path) ||
      [...seen].some((parent) => path.startsWith(`${parent}/`) || parent.startsWith(`${path}/`))
    )
      fail("contract paths must be unique, non-overlapping directories");
    seen.add(path);
  }
  return config;
}

async function treeFiles(root, label) {
  await realDirectory(root, label);
  const files = [];
  async function walk(directory, prefix = "") {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name);
      const child = prefix ? `${prefix}/${entry.name}` : entry.name;
      if (entry.isSymbolicLink()) fail(`symbolic link found at ${label}/${child}`);
      if (entry.isDirectory()) await walk(path, child);
      else if (entry.isFile()) files.push(child);
      else fail(`unsupported filesystem entry at ${label}/${child}`);
    }
  }
  await walk(root);
  return files.sort();
}

async function fileRecord(root, path) {
  const contents = await readFile(join(root, ...path.split("/")));
  return {
    path,
    size: contents.byteLength,
    sha256: createHash("sha256").update(contents).digest("hex"),
  };
}

function createMetadata(options, config) {
  const clientsRepository = requiredString(options.clientsRepository, "clients repository");
  if (!repositoryPattern.test(clientsRepository)) fail("clients repository must be owner/name");
  return {
    schema: 1,
    clientsRepository,
    filaments: { repository: config.repository, sha: sha(options.filamentsSha, "Filaments SHA") },
    pullRequest: {
      number: Number(positiveInteger(options.prNumber, "PR number")),
      base: sha(options.baseSha, "base SHA"),
      head: sha(options.headSha, "head SHA"),
      merge: sha(options.mergeSha, "merge SHA"),
    },
    producer: {
      runId: positiveInteger(options.producerRunId, "producer run ID"),
      runAttempt: positiveInteger(options.producerRunAttempt, "producer run attempt"),
    },
  };
}

export async function createContractArtifact(options) {
  const config = await configuredPaths();
  const filamentsRoot = absolutePath(options?.filamentsRoot, "Filaments root");
  const outputRoot = absolutePath(options?.outputRoot, "artifact output root");
  const metadata = createMetadata(options ?? {}, config);
  const existingOutput = await info(outputRoot, "artifact output root");
  if (existingOutput) {
    if (!existingOutput.isDirectory()) fail("artifact output root must be a directory");
    if ((await readdir(outputRoot)).length !== 0) fail("artifact output root must be empty");
  }
  const records = [];
  for (const allowed of config.paths) {
    const source = await descendantDirectory(filamentsRoot, allowed, "Filaments contract");
    for (const file of await treeFiles(source, `Filaments contract ${allowed}`)) {
      const path = artifactPath(`${allowed}/${file}`);
      records.push(await fileRecord(filamentsRoot, path));
    }
  }
  records.sort((left, right) => (left.path < right.path ? -1 : left.path > right.path ? 1 : 0));
  await mkdir(outputRoot, { recursive: true });
  for (const record of records) {
    const destination = join(outputRoot, ...record.path.split("/"));
    await mkdir(resolve(destination, ".."), { recursive: true });
    await copyFile(join(filamentsRoot, ...record.path.split("/")), destination);
  }
  const manifest = { ...metadata, allowlistedPaths: [...config.paths].sort(), files: records };
  await writeFile(join(outputRoot, "manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
  return manifest;
}

function exactKeys(value, keys, label) {
  if (!value || typeof value !== "object" || Array.isArray(value)) fail(`invalid ${label}`);
  const actual = Object.keys(value).sort();
  const expected = [...keys].sort();
  if (JSON.stringify(actual) !== JSON.stringify(expected)) fail(`invalid ${label}`);
}

function validateManifest(value, config) {
  exactKeys(
    value,
    [
      "schema",
      "clientsRepository",
      "filaments",
      "pullRequest",
      "producer",
      "allowlistedPaths",
      "files",
    ],
    "manifest",
  );
  if (value.schema !== 1) fail("unsupported manifest schema");
  if (
    !repositoryPattern.test(requiredString(value.clientsRepository, "manifest clients repository"))
  )
    fail("invalid manifest clients repository");
  exactKeys(value.filaments, ["repository", "sha"], "manifest filaments metadata");
  if (value.filaments.repository !== config.repository)
    fail("invalid manifest Filaments repository");
  sha(value.filaments.sha, "manifest Filaments SHA");
  exactKeys(
    value.pullRequest,
    ["number", "base", "head", "merge"],
    "manifest pull request metadata",
  );
  positiveInteger(value.pullRequest.number, "manifest PR number");
  for (const key of ["base", "head", "merge"]) sha(value.pullRequest[key], `manifest ${key} SHA`);
  exactKeys(value.producer, ["runId", "runAttempt"], "manifest producer metadata");
  positiveInteger(value.producer.runId, "manifest producer run ID");
  positiveInteger(value.producer.runAttempt, "manifest producer run attempt");
  if (
    !Array.isArray(value.allowlistedPaths) ||
    JSON.stringify(value.allowlistedPaths) !== JSON.stringify([...config.paths].sort())
  )
    fail("invalid manifest allowlisted paths");
  if (!Array.isArray(value.files)) fail("invalid manifest files");
  let previous = "";
  const seen = new Set();
  for (const record of value.files) {
    exactKeys(record, ["path", "size", "sha256"], "manifest file record");
    const path = artifactPath(record.path);
    if (path <= previous || seen.has(path)) fail("manifest files must be sorted and unique");
    if (!config.paths.some((prefix) => path.startsWith(`${prefix}/`)))
      fail(`unexpected manifest artifact path: ${path}`);
    if (!Number.isSafeInteger(record.size) || record.size < 0) fail("invalid manifest file size");
    if (typeof record.sha256 !== "string" || !/^[0-9a-f]{64}$/.test(record.sha256))
      fail("invalid manifest file SHA-256");
    previous = path;
    seen.add(path);
  }
  return value;
}

function checkExpected(manifest, options) {
  const pairs = [
    ["clientsRepository", manifest.clientsRepository, options.expectedClientsRepository],
    ["Filaments SHA", manifest.filaments.sha, options.expectedFilamentsSha],
    ["PR number", String(manifest.pullRequest.number), options.expectedPrNumber],
    ["base SHA", manifest.pullRequest.base, options.expectedBaseSha],
    ["head SHA", manifest.pullRequest.head, options.expectedHeadSha],
    ["merge SHA", manifest.pullRequest.merge, options.expectedMergeSha],
    ["producer run ID", manifest.producer.runId, options.expectedProducerRunId],
    ["producer run attempt", manifest.producer.runAttempt, options.expectedProducerRunAttempt],
  ];
  for (const [label, actual, expected] of pairs) {
    if (expected === undefined) continue;
    const normalized = label.includes("SHA")
      ? sha(expected, `expected ${label}`)
      : requiredString(String(expected), `expected ${label}`);
    if (String(actual) !== normalized) fail(`${label} mismatch`);
  }
}

async function artifactEntries(root) {
  await realDirectory(root, "artifact root");
  const files = [];
  const directories = [];
  async function walk(directory, prefix = "") {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const child = prefix ? `${prefix}/${entry.name}` : entry.name;
      const path = join(directory, entry.name);
      if (entry.isSymbolicLink()) fail(`symbolic link found at artifact path ${child}`);
      if (entry.isDirectory()) {
        directories.push(child);
        await walk(path, child);
      } else if (entry.isFile()) files.push(child);
      else fail(`unsupported filesystem entry at artifact path ${child}`);
    }
  }
  await walk(root);
  return { directories: directories.sort(), files: files.sort() };
}

export async function verifyContractArtifact(options) {
  const config = await configuredPaths();
  const root = absolutePath(options?.artifactRoot, "artifact root");
  let manifest;
  try {
    manifest = JSON.parse(await readFile(join(root, "manifest.json"), "utf8"));
  } catch (error) {
    fail(`cannot read manifest: ${error.message}`);
  }
  validateManifest(manifest, config);
  checkExpected(manifest, options ?? {});
  const expected = new Set(["manifest.json", ...manifest.files.map((record) => record.path)]);
  const { files, directories } = await artifactEntries(root);
  for (const path of files) if (!expected.has(path)) fail(`unexpected artifact path: ${path}`);
  for (const path of expected) if (!files.includes(path)) fail(`missing artifact path: ${path}`);
  const expectedDirectories = new Set();
  for (const path of manifest.files.map((record) => record.path)) {
    const components = path.split("/");
    for (let index = 1; index < components.length; index += 1)
      expectedDirectories.add(components.slice(0, index).join("/"));
  }
  for (const path of directories)
    if (!expectedDirectories.has(path)) fail(`unexpected artifact directory: ${path}`);
  for (const record of manifest.files) {
    const actualRecord = await fileRecord(root, record.path);
    if (actualRecord.size !== record.size) fail(`size mismatch for ${record.path}`);
    if (actualRecord.sha256 !== record.sha256) fail(`hash mismatch for ${record.path}`);
  }
  return manifest;
}

function parseArguments(argv) {
  const [command, ...flags] = argv;
  if (!["create", "verify"].includes(command) || flags.length % 2 !== 0)
    fail("invalid command arguments");
  const values = {};
  for (let index = 0; index < flags.length; index += 2) {
    const flag = flags[index];
    const value = flags[index + 1];
    if (!flag.startsWith("--") || value === undefined || Object.hasOwn(values, flag))
      fail("invalid command arguments");
    values[flag.slice(2).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase())] = value;
  }
  return { command, values };
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const { command, values } = parseArguments(process.argv.slice(2));
  if (command === "create") await createContractArtifact(values);
  else await verifyContractArtifact(values);
}
