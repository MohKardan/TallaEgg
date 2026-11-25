"use client";

import { useCallback, useEffect, useState } from "react";

export interface PublicSessionUser {
  telegramId: number;
  username?: string;
  firstName?: string;
  lastName?: string;
  role: string;
  status: string;
}

interface SessionSnapshot {
  user: PublicSessionUser | null;
  loaded: boolean;
}

let cachedSnapshot: SessionSnapshot = {
  user: null,
  loaded: false,
};
let inFlightRequest: Promise<PublicSessionUser | null> | null = null;
const listeners = new Set<(snapshot: SessionSnapshot) => void>();

function notifyListeners() {
  listeners.forEach((listener) => listener(cachedSnapshot));
}

async function fetchSessionFromServer(): Promise<PublicSessionUser | null> {
  try {
    const res = await fetch("/api/auth/me", { cache: "no-store" });
    if (!res.ok) {
      return null;
    }
    const data = await res.json().catch(() => null);
    if (data?.authenticated && data?.user) {
      return data.user as PublicSessionUser;
    }
    return null;
  } catch {
    return null;
  }
}

export async function loadSharedSession(
  forceRefresh = false
): Promise<PublicSessionUser | null> {
  if (!forceRefresh && cachedSnapshot.loaded) {
    return cachedSnapshot.user;
  }

  if (!inFlightRequest) {
    inFlightRequest = fetchSessionFromServer()
      .then((user) => {
        cachedSnapshot = { user, loaded: true };
        notifyListeners();
        return user;
      })
      .finally(() => {
        inFlightRequest = null;
      });
  }

  return inFlightRequest;
}

export function setSessionCache(user: PublicSessionUser | null): void {
  cachedSnapshot = { user, loaded: true };
  notifyListeners();
}

export function invalidateSessionCache(): void {
  cachedSnapshot = { user: null, loaded: false };
  notifyListeners();
}

export function useSession() {
  const [snapshot, setSnapshot] = useState<SessionSnapshot>(cachedSnapshot);

  useEffect(() => {
    const listener = (next: SessionSnapshot) => setSnapshot(next);
    listeners.add(listener);

    if (!cachedSnapshot.loaded) {
      void loadSharedSession(false);
    } else {
      setSnapshot(cachedSnapshot);
    }

    return () => {
      listeners.delete(listener);
    };
  }, []);

  const refresh = useCallback(() => loadSharedSession(true), []);

  return {
    user: snapshot.user,
    loading: !snapshot.loaded,
    refresh,
  };
}
