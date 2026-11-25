export type UserStatusName =
  | "Pending"
  | "Approved"
  | "Rejected"
  | "Suspended"
  | "Blocked"
  | "Unknown";

export type UserRoleName =
  | "RegularUser"
  | "Accountant"
  | "Admin"
  | "SuperAdmin"
  | "User"
  | "Unknown";

export interface UserDto {
  id: string;
  telegramId: number;
  username?: string;
  firstName?: string;
  lastName?: string;
  status: UserStatusName;
  statusCode: number;
  isActive: boolean;
  createdAt: string;
  lastActiveAt?: string;
  phoneNumber?: string;
  role: UserRoleName;
}

const statusMap: Record<number, UserStatusName> = {
  0: "Pending",
  1: "Approved",
  2: "Rejected",
  3: "Suspended",
  4: "Blocked",
};

// Mirrors src/TallaEgg/TallaEgg.Core/Enums/User/UserRole.cs
const roleMap: Record<number, UserRoleName> = {
  0: "RegularUser",
  1: "Accountant",
  2: "Admin",
  3: "SuperAdmin",
  4: "User",
};

export function parseUserStatus(rawStatus: unknown): {
  name: UserStatusName;
  code: number;
} {
  if (typeof rawStatus === "number" && Number.isInteger(rawStatus)) {
    return {
      name: statusMap[rawStatus] || "Unknown",
      code: rawStatus,
    };
  }
  if (typeof rawStatus === "string") {
    const trimmed = rawStatus.trim();
    if (/^\d+$/.test(trimmed)) {
      const numeric = parseInt(trimmed, 10);
      if (statusMap[numeric]) {
        return { name: statusMap[numeric], code: numeric };
      }
    }
    const matched = Object.entries(statusMap).find(
      ([, name]) => name.toLowerCase() === trimmed.toLowerCase()
    );
    if (matched) {
      return { name: matched[1], code: Number(matched[0]) };
    }
  }
  return { name: "Unknown", code: -1 };
}

export function parseUserRole(rawRole: unknown): UserRoleName {
  if (rawRole === undefined || rawRole === null) {
    return "RegularUser";
  }
  if (typeof rawRole === "number" && Number.isInteger(rawRole)) {
    return roleMap[rawRole] || "Unknown";
  }
  if (typeof rawRole === "string") {
    const trimmed = rawRole.trim();
    if (!trimmed) {
      return "RegularUser";
    }
    if (/^\d+$/.test(trimmed)) {
      const numeric = parseInt(trimmed, 10);
      return roleMap[numeric] || "Unknown";
    }
    const matched = Object.values(roleMap).find(
      (name) => name.toLowerCase() === trimmed.toLowerCase()
    );
    if (matched) {
      return matched;
    }
  }
  return "Unknown";
}

export async function getUserByTelegramId(
  telegramId: number
): Promise<{ user: UserDto | null; error?: string }> {
  const baseUrl = (
    process.env.USERS_API_URL || "http://127.0.0.1:5136"
  ).replace(/\/+$/, "");
  const apiKey = process.env.INTERNAL_API_KEY;

  const headers: Record<string, string> = {
    Accept: "application/json",
  };
  if (apiKey) {
    headers["X-Api-Key"] = apiKey;
  }

  try {
    const res = await fetch(`${baseUrl}/api/user/${encodeURIComponent(String(telegramId))}`, {
      method: "GET",
      headers,
      cache: "no-store",
      signal: AbortSignal.timeout(8000),
    });

    // 404: user truly does not exist in the database
    if (res.status === 404) {
      return { user: null };
    }

    // 400: schema or validation error - do not silently mask as missing user
    if (res.status === 400) {
      const errJson = await res.json().catch(() => null);
      if (
        errJson?.message?.includes("User not found") ||
        errJson?.message?.includes("کاربر یافت نشد")
      ) {
        return { user: null };
      }
      return {
        user: null,
        error: `Users.Api rejected request (HTTP 400): ${errJson?.message || res.statusText}`,
      };
    }

    if (!res.ok) {
      return {
        user: null,
        error: `Users.Api returned HTTP ${res.status}: ${res.statusText}`,
      };
    }

    const json = await res.json();
    if (!json.success || !json.data) {
      return { user: null, error: json.message || "Failed to parse user data." };
    }

    const raw = json.data;
    const { name: statusName, code: statusCode } = parseUserStatus(raw.status);

    const user: UserDto = {
      id: raw.id,
      telegramId: raw.telegramId,
      username: raw.username || undefined,
      firstName: raw.firstName || undefined,
      lastName: raw.lastName || undefined,
      status: statusName,
      statusCode,
      isActive: Boolean(raw.isActive),
      createdAt: raw.createdAt,
      lastActiveAt: raw.lastActiveAt || undefined,
      phoneNumber: raw.phoneNumber || undefined,
      role: parseUserRole(raw.role),
    };

    return { user };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return {
      user: null,
      error: `Network error connecting to Users.Api: ${message}`,
    };
  }
}
