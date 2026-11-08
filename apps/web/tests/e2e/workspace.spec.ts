import { passwords, signIn } from "./helpers";
import { expect, test, type Request } from "@playwright/test";
import { addDays, dayLabel } from "../../src/lib/schedule-view";

test.use({ trace: "off", screenshot: "off", video: "off" });

test("manager and employee complete a saved staffing workflow", async ({
  page,
  context,
}, testInfo) => {
  test.setTimeout(60000);
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
    .getByRole("combobox", { name: "Required skill", exact: true })
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
  await expect(page).toHaveURL(/\/$/);
  const createdCard = page
    .locator(".saved-day")
    .filter({
      has: page.getByRole("heading", { name: dayLabel(date), exact: true }),
    })
    .locator("[data-shift-id]")
    .filter({
      has: page.getByText(`${hour}:00â€“${hour + 2}:00`, { exact: true }),
    });
  await expect(createdCard).toHaveCount(1);
  await expect(createdCard).toContainText("Barista");
  await expect(createdCard).toContainText("Open shift");
  const createdId = await createdCard.getAttribute("data-shift-id");
  expect(createdId).toBeTruthy();
  const row = page.locator(`[data-shift-id="${createdId}"]`);
  await row
    .getByRole("button", { name: "Assign employee", exact: true })
    .click();
  await page
    .getByRole("combobox", { name: "Employee", exact: true })
    .selectOption("10000000-0000-0000-0000-000000000004");
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
  await expect(row).toContainText("Coverage requested Â· Pending");
  await addAvailability.click();
  const otherTab = await context.newPage();
  await otherTab.goto("/");
  await otherTab.getByRole("button", { name: "Sign out", exact: true }).click();
  await signIn(otherTab, "manager@northline.example", credentials.manager);
  await expect(
    otherTab.getByRole("heading", { name: "Set the floor for a good day." }),
  ).toBeVisible();
  let staleWrites = 0;
  const observe = (request: Request) => {
    if (request.method() === "POST" && request.url().endsWith("/availability"))
      staleWrites++;
  };
  page.on("request", observe);
  await dialog
    .getByRole("button", { name: "Save availability", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Sign in to your workspace" }),
  ).toBeVisible();
  await expect(
    page
      .getByRole("region", { name: "Sign in to your workspace", exact: true })
      .getByRole("alert"),
  ).toContainText("changed or expired");
  expect(staleWrites).toBe(0);
  page.off("request", observe);
  await otherTab.close();
  await signIn(page, "manager@northline.example", credentials.manager);
  await expect(row).toContainText("Coverage requested Â· Pending");
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
