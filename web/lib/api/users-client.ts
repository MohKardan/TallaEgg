export type UserStatusName =
  | "Pending"
  | "Approved"
  | "Rejected"
  | "Suspended"
  | "Blocked"
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
  role: string;
}

const statusMap: Record<number, UserStatusName> = {
  0: "Pending",
  1: "Approved",
  2: "Rejected",
  3: "Suspended",
  4: "Blocked",
};

export function parseUserStatus(rawStatus: any): {
  name: UserStatusName;
  code: number;
} {
  if (typeof rawStatus === "number") {
    return {
      name: statusMap[rawStatus] || "Unknown",
      code: rawStatus,
    };
  }
  if (typeof rawStatus === "string") {
    const numeric = parseInt(rawStatus, 10);
    if (!isNaN(numeric) && statusMap[numeric]) {
      return { name: statusMap[numeric], code: numeric };
    }
    const matched = Object.entries(statusMap).find(
      ([, name]) => name.toLowerCase() === rawStatus.toLowerCase()
    );
    if (matched) {
      return { name: matched[1], code: Number(matched[0]) };
    }
  }
  return { name: "Unknown", code: -1 };
}

export async function getUserByTelegramId(
  telegramId: number
): Promise<{ user: UserDto | null; error?: string }> {
  const baseUrl = process.env.USERS_API_URL || "http://127.0.0.1:5136";
  const apiKey = process.env.INTERNAL_API_KEY;

  const headers: Record<string, string> = {
    Accept: "application/json",
  };
  if (apiKey) {
    headers["X-Api-Key"] = apiKey;
  }

  try {
    const res = await fetch(`${baseUrl}/api/user/${telegramId}`, {
      method: "GET",
      headers,
      cache: "no-store",
    });

    if (res.status === 400 || res.status === 404) {
      return { user: null };
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
      role: String(raw.role ?? "RegularUser"),
    };

    return { user };
  } catch (err: any) {
    return {
      user: null,
      error: `Network error connecting to Users.Api: ${err.message}`,
    };
  }
}
