import { describe, it } from "node:test";
import assert from "node:assert";
import { parseUserRole, parseUserStatus } from "../lib/api/users-client";

describe("Users API Enum Parsing", () => {
  it("parses numeric and string UserRole values accurately", () => {
    assert.strictEqual(parseUserRole(0), "RegularUser");
    assert.strictEqual(parseUserRole(1), "Accountant");
    assert.strictEqual(parseUserRole(2), "Admin");
    assert.strictEqual(parseUserRole(3), "SuperAdmin");
    assert.strictEqual(parseUserRole(4), "User");

    assert.strictEqual(parseUserRole("0"), "RegularUser");
    assert.strictEqual(parseUserRole("2"), "Admin");
    assert.strictEqual(parseUserRole("superadmin"), "SuperAdmin");
    assert.strictEqual(parseUserRole("Accountant"), "Accountant");

    assert.strictEqual(parseUserRole(undefined), "RegularUser");
    assert.strictEqual(parseUserRole(null), "RegularUser");
    assert.strictEqual(parseUserRole(99), "Unknown");
    assert.strictEqual(parseUserRole("InvalidRole"), "Unknown");
  });

  it("parses numeric and string UserStatus values accurately", () => {
    assert.deepStrictEqual(parseUserStatus(0), { name: "Pending", code: 0 });
    assert.deepStrictEqual(parseUserStatus(1), { name: "Approved", code: 1 });
    assert.deepStrictEqual(parseUserStatus(2), { name: "Rejected", code: 2 });
    assert.deepStrictEqual(parseUserStatus(3), { name: "Suspended", code: 3 });
    assert.deepStrictEqual(parseUserStatus(4), { name: "Blocked", code: 4 });

    assert.deepStrictEqual(parseUserStatus("1"), { name: "Approved", code: 1 });
    assert.deepStrictEqual(parseUserStatus("blocked"), {
      name: "Blocked",
      code: 4,
    });
    assert.deepStrictEqual(parseUserStatus("NonExistent"), {
      name: "Unknown",
      code: -1,
    });
  });
});
