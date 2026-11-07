import { readFileSync } from "node:fs";
import { expect, test, type Page } from "@playwright/test";
import { addDays } from "../../src/lib/schedule-view";

test.use({ trace: "off", screenshot: "off", video: "off" });

function passwords() {
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

async function signIn(page: Page, email: string, password: string) {
  await expect(
    page.getByRole("heading", { name: "Sign in to your workspace" }),
  ).toBeVisible();
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
}

test("manager and employee complete a saved staffing workflow", async ({
  page,
}, testInfo) => {
  const credentials = passwords();
  await page.goto("/");
  await signIn(page, "manager@northline.example", credentials.manager);
  await expect(
    page.getByRole("heading", { name: "Set the floor for a good day." }),
  ).toBeVisible();
  const week = await page
    .getByLabel("Week beginning", { exact: true })
    .inputValue();
  const date = addDays(week, testInfo.project.name === "mobile" ? 3 : 2);
  const hour = 14 + testInfo.retry * 2;
  const startsAt = `${date}T${hour}:00`;
  const endsAt = `${date}T${hour + 2}:00`;
  await page.getByRole("button", { name: "Create shift", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await page
    .getByLabel("Starts (location time)", { exact: true })
    .fill(startsAt);
  await page.getByLabel("Ends (location time)", { exact: true }).fill(endsAt);
  await page
    .getByLabel("Required skill", { exact: true })
    .selectOption({ label: "Barista" });
  const createdResponse = page.waitForResponse(
    (response) =>
      response.url().endsWith("/shifts") &&
      response.request().method() === "POST",
  );
  await dialog
    .getByRole("button", { name: "Create shift", exact: true })
    .click();
  const response = await createdResponse;
  expect(response.status()).toBe(201);
  const created = (await response.json()) as { id: string };
  const row = page.locator(`[data-shift-id="${created.id}"]`);
  await row
    .getByRole("button", { name: "Assign employee", exact: true })
    .click();
  await page
    .getByLabel("Employee", { exact: true })
    .selectOption({ label: "Morgan Park" });
  await dialog
    .getByRole("button", { name: "Save assignment", exact: true })
    .click();
  await expect(row).toContainText("Morgan Park");
  await page.reload();
  await expect(row).toContainText("Morgan Park");
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);

  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await signIn(page, "employee@northline.example", credentials.employee);
  await expect(
    page.getByRole("heading", { name: "Your week, in view." }),
  ).toBeVisible();
  await expect(row).toContainText("Morgan Park");
  await expect(
    page.getByRole("button", { name: "Create shift", exact: true }),
  ).toHaveCount(0);
  const addAvailability = page.getByRole("button", {
    name: "Add availability",
    exact: true,
  });
  await addAvailability.click();
  await page.keyboard.press("Escape");
  await expect(dialog).not.toBeVisible();
  await expect(addAvailability).toBeFocused();
  await addAvailability.click();
  await page
    .getByLabel("Starts (location time)", { exact: true })
    .fill(startsAt);
  await page.getByLabel("Ends (location time)", { exact: true }).fill(endsAt);
  await dialog
    .getByRole("button", { name: "Save availability", exact: true })
    .click();
  await expect(
    page.getByText("Saved. The schedule has been refreshed.", { exact: true }),
  ).toBeFocused();
  await row
    .getByRole("button", { name: "Request coverage", exact: true })
    .click();
  await expect(dialog).toContainText("Your shift stays assigned to you.");
  await dialog
    .getByRole("button", { name: "Submit coverage request", exact: true })
    .click();
  await expect(row).toContainText("Coverage requested · Pending");
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
  await signIn(page, "manager@northline.example", credentials.manager);
  await expect(row).toContainText("Coverage requested · Pending");
  await expect(page.locator("#requests")).toContainText("Morgan Park");
  await expect(
    row.getByRole("button", { name: "Request pending", exact: true }),
  ).toBeDisabled();
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  await page.getByRole("button", { name: "Sign out", exact: true }).click();
});
