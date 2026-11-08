import { expect, test } from "@playwright/test";
import { addDays, dayLabel } from "../../src/lib/schedule-view";
import { passwords, signIn } from "./helpers";

test.use({ trace: "off", screenshot: "off", video: "off" });

test("coverage resolution refreshes another session and the lab resets open forms", async ({
  page,
  browser,
  context,
}, testInfo) => {
  test.setTimeout(120000);
  const credentials = passwords();
  const employeeContext = await browser.newContext();
  const employee = await employeeContext.newPage();
  try {
    await page.goto("/");
    await signIn(page, "manager@northline.example", credentials.manager);
    await expect(
      page.getByText("Live updates connected", { exact: true }),
    ).toBeVisible();
    const week = await page
      .getByLabel("Week beginning", { exact: true })
      .inputValue();
    const date = addDays(week, testInfo.project.name === "mobile" ? 6 : 5);
    const hour = 8 + testInfo.retry * 2;
    await page
      .getByRole("button", { name: "Create shift", exact: true })
      .click();
    const dialog = page.getByRole("dialog");
    await dialog
      .getByLabel("Starts (location time)", { exact: true })
      .fill(`${date}T${hour.toString().padStart(2, "0")}:00`);
    await dialog
      .getByLabel("Ends (location time)", { exact: true })
      .fill(`${date}T${(hour + 2).toString().padStart(2, "0")}:00`);
    await dialog
      .getByRole("combobox", { name: "Required skill", exact: true })
      .selectOption({ label: "Barista" });
    await dialog
      .getByRole("button", { name: "Create shift", exact: true })
      .click();
    const row = page
      .locator(".saved-day")
      .filter({
        has: page.getByRole("heading", { name: dayLabel(date), exact: true }),
      })
      .locator("[data-shift-id]")
      .filter({
        hasText: `${hour.toString().padStart(2, "0")}:00–${(hour + 2).toString().padStart(2, "0")}:00`,
      });
    await expect(row).toHaveCount(1);
    const id = await row.getAttribute("data-shift-id");
    await row
      .getByRole("button", { name: "Assign employee", exact: true })
      .click();
    await dialog
      .getByRole("combobox", { name: "Employee", exact: true })
      .selectOption("10000000-0000-0000-0000-000000000004");
    await dialog
      .getByRole("button", { name: "Save assignment", exact: true })
      .click();
    await expect(row).toContainText("Morgan Park");
    await employee.goto("/");
    await signIn(employee, "employee@northline.example", credentials.employee);
    await expect(
      employee.getByText("Live updates connected", { exact: true }),
    ).toBeVisible();
    const own = employee.locator(`[data-shift-id="${id}"]`);
    await own
      .getByRole("button", { name: "Request coverage", exact: true })
      .click();
    await employee
      .getByRole("dialog")
      .getByRole("button", { name: "Submit coverage request", exact: true })
      .click();
    await expect(row).toContainText("Coverage requested · Pending");
    await employee
      .locator(`#requests [data-request-shift-id="${id}"]`)
      .getByRole("button", { name: "Cancel request", exact: true })
      .click();
    await employee
      .getByRole("dialog")
      .getByRole("button", { name: "Cancel request", exact: true })
      .click();
    await expect(
      page.locator(`#requests [data-request-shift-id="${id}"]`),
    ).toContainText("Cancelled");
    await own
      .getByRole("button", { name: "Request coverage", exact: true })
      .click();
    await employee
      .getByRole("dialog")
      .getByRole("button", { name: "Submit coverage request", exact: true })
      .click();
    const pending = page
      .locator(`#requests [data-request-shift-id="${id}"]`)
      .filter({
        has: page.getByRole("button", {
          name: "Review replacements",
          exact: true,
        }),
      });
    await pending
      .getByRole("button", { name: "Review replacements", exact: true })
      .click();
    await dialog
      .getByRole("combobox", { name: "Replacement", exact: true })
      .selectOption("10000000-0000-0000-0000-000000000003");
    await dialog
      .getByRole("button", { name: "Approve replacement", exact: true })
      .click();
    await expect(row).toContainText("Alex Kim");
    // This independent employee session never presses Refresh or reloads after approval.
    await expect(
      employee
        .locator(`#requests [data-request-shift-id="${id}"]`)
        .filter({ hasText: "Approved" }),
    ).toHaveCount(1);
    await expect(own).toHaveCount(0);
    await expect(
      employee
        .locator(`#requests [data-request-shift-id="${id}"]`)
        .filter({ hasText: "Approved" }),
    ).toContainText("Alex Kim");

    await page
      .getByRole("button", { name: "Open Friday Rush lab", exact: true })
      .click();
    await expect(
      page.getByRole("button", { name: "Reset lab", exact: true }),
    ).toBeVisible();
    await page.getByRole("button", { name: "Reset lab", exact: true }).click();
    await dialog
      .getByRole("button", { name: "Restore baseline", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Apply Friday Rush", exact: true })
      .click();
    await expect(page.locator("#coverage")).toContainText("2 people short");
    await page
      .locator("#requests")
      .getByRole("button", { name: "Review replacements", exact: true })
      .click();
    await expect(dialog).toContainText("Why others are unavailable");
    const replacement = dialog.getByRole("combobox", {
      name: "Replacement",
      exact: true,
    });
    await expect(replacement.locator("option")).toHaveCount(3);
    await replacement.selectOption(
      (await replacement.locator("option").nth(1).getAttribute("value"))!,
    );
    await dialog
      .getByRole("button", { name: "Approve replacement", exact: true })
      .click();
    await expect(page.locator("#coverage")).toContainText("1 person short");
    const standby = page.locator(".saved-shift.open");
    await expect(standby).toHaveCount(1);
    await standby
      .getByRole("button", { name: "Assign employee", exact: true })
      .click();
    const selection = dialog.getByRole("combobox", {
      name: "Employee",
      exact: true,
    });
    await expect(selection.locator("option")).toHaveCount(2);
    await selection.selectOption(
      (await selection.locator("option").nth(1).getAttribute("value"))!,
    );
    await dialog
      .getByRole("button", { name: "Save assignment", exact: true })
      .click();
    await expect(page.locator("#coverage")).toContainText(
      "All staffing targets for this week are covered.",
    );
    await expect(page.locator("#audit")).toContainText(
      "CoverageRequest · Approved",
    );
    const otherTab = await context.newPage();
    await otherTab.goto("/");
    await otherTab
      .getByRole("combobox", { name: "Organization", exact: true })
      .selectOption({ label: "Friday Rush Lab" });
    await expect(
      otherTab.getByText("Live updates connected", { exact: true }),
    ).toBeVisible();
    await otherTab
      .getByRole("button", { name: "Create shift", exact: true })
      .click();
    await expect(otherTab.getByRole("dialog")).toBeVisible();
    await page.getByRole("button", { name: "Reset lab", exact: true }).click();
    await dialog
      .getByRole("button", { name: "Restore baseline", exact: true })
      .click();
    await expect(otherTab.getByRole("dialog")).toHaveCount(0);
    await expect(
      otherTab.getByText(
        "The lab was reset. Old forms were closed and the new baseline is loaded.",
        { exact: true },
      ),
    ).toBeVisible();
    await otherTab.close();
    await page
      .getByRole("button", { name: "Return to normal workspace", exact: true })
      .click();
    await expect(row).toContainText("Alex Kim");
    expect(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
    ).toBe(true);
  } finally {
    await employeeContext.close();
  }
});
