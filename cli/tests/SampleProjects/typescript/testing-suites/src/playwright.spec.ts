import { test, expect } from '@playwright/test';

test.describe("Playwright E2E Suite", () => {
    test("should open dashboard and fetch stats", async ({ page, request }) => {
        await page.goto("https://portal.example.com/admin");
        await request.get("https://api.example.com/stats");
    });
});
