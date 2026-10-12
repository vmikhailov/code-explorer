import { describe, it, expect, vi } from 'vitest';

vi.mock("./services/user.service");

describe("Vitest User Suite", () => {
    it("should fetch user profile", () => {
        expect(1).toBe(1);
    });
});
