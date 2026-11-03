import { randomBytes } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const root = new URL("../", import.meta.url);
const template = await readFile(new URL(".env.example", root), "utf8");
const contents = template.replace(/^POSTGRES_PASSWORD=$/m, `POSTGRES_PASSWORD=${randomBytes(24).toString("hex")}`);
try {
  await writeFile(new URL(".env", root), contents, { flag: "wx", mode: 0o600 });
  console.log(`Created ${fileURLToPath(new URL(".env", root))} with generated local credentials.`);
} catch (error) {
  if (error.code !== "EEXIST") throw error;
  console.log("Existing .env preserved.");
}
