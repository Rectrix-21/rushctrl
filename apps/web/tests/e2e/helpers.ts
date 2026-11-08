import { readFileSync } from "node:fs";
import { expect, type Page } from "@playwright/test";

export function passwords() {
  const config = Object.fromEntries(
    readFileSync(new URL("../../../../.env", import.meta.url), "utf8")
      .split(/\r?\n/)
      .filter((line) => /^[A-Z_]+=/.test(line))
      .map((line) => {
        const index = line.indexOf("=");
        return [line.slice(0, index), line.slice(index + 1)];
      }),
  );
  const manager =
    process.env.DEMO_MANAGER_PASSWORD ?? config.DEMO_MANAGER_PASSWORD;
  const employee =
    process.env.DEMO_EMPLOYEE_PASSWORD ?? config.DEMO_EMPLOYEE_PASSWORD;
  if (!manager || !employee)
    throw new Error(
      "Run local setup/dev or the CI demo seed before the workflow test.",
    );
  return { manager, employee };
}

export async function signIn(page: Page, email: string, password: string) {
  await expect(
    page.getByRole("heading", { name: "Sign in to your workspace" }),
  ).toBeVisible();
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}
