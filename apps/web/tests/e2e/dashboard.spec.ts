import { expect, test } from "@playwright/test";

test("sample dashboard supports views and a keyboard-operated coverage panel", async ({ page }) => {
  await page.goto("/");
  await expect(page.getByRole("heading", { name: "Ready for the rush." })).toBeVisible();
  await expect(page.getByText("Milestone 0 · Synthetic preview")).toBeVisible();
  await page.getByRole("button", { name: "Week", exact: true }).click();
  await expect(page.getByText("Week of November 2–6")).toBeVisible();
  await page.getByRole("button", { name: "Day", exact: true }).click();
  const review = page.getByRole("button", { name: "Review coverage" });
  await review.click();
  await expect(page.getByRole("dialog")).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).not.toBeVisible();
  await expect(review).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
});

test("failed readiness never appears as a healthy connection", async ({ page }) => {
  await page.route("**/api/v1/system", route => route.fulfill({ json: { name: "RUSHCTRL", version: "0.1.0", milestone: 0 } }));
  await page.route("**/health/ready", route => route.fulfill({ status: 503, json: { status: "unhealthy" } }));
  await page.goto("/");
  await expect(page.getByRole("status")).toHaveText("Database unavailable");
});
