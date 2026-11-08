import { randomBytes } from "node:crypto";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";

const root = new URL("../", import.meta.url);
const template = await readFile(new URL(".env.example", root), "utf8");
const envPath = new URL(".env", root);
let contents;
try { contents = await readFile(envPath, "utf8"); }
catch (error) { if (error.code !== "ENOENT") throw error; contents = template; }
for (const key of ["POSTGRES_PASSWORD", "DEMO_MANAGER_PASSWORD", "DEMO_EMPLOYEE_PASSWORD"]) {
  const expression = new RegExp(`^${key}=(.*)$`, "m");
  const existing = contents.match(expression);
  if (existing?.[1].trim()) continue;
  const value = key === "POSTGRES_PASSWORD" ? randomBytes(24).toString("hex") : `Rc!${randomBytes(24).toString("base64url")}7z`;
  contents = existing ? contents.replace(expression, `${key}=${value}`) : `${contents.trimEnd()}\n${key}=${value}\n`;
}
await writeFile(envPath, contents, { mode: 0o600 });
await mkdir(new URL(".local/", root), { recursive: true });
const password = (key) => contents.match(new RegExp(`^${key}=(.*)$`, "m"))[1];
await writeFile(new URL(".local/demo-accounts.txt", root),
  `RUSHCTRL local synthetic accounts\n\nManager: manager@northline.example\nPassword: ${password("DEMO_MANAGER_PASSWORD")}\n\nEmployee: employee@northline.example\nPassword: ${password("DEMO_EMPLOYEE_PASSWORD")}\n\nPasswords apply when accounts are first created; changing .env does not reset existing accounts.\n`, { mode: 0o600 });
console.log(`Local credentials ready in ${fileURLToPath(envPath)}. Existing nonempty values preserved.`);
console.log("Open .local/demo-accounts.txt for development sign-in details.");
