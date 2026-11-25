export type AssetCategory = "gold" | "fiat" | "crypto" | "credit" | "other";

export interface RawWalletDto {
  asset: string;
  balance: number;
  lockedBalance: number;
  updatedAt: string;
}

export interface WalletItem {
  asset: string;
  balance: number;
  lockedBalance: number;
  availableBalance: number;
  totalBalance: number;
  updatedAt: string;
  isCreditLedger: boolean;
  persianLabel: string;
  unit: string;
  category: AssetCategory;
  decimalPlaces: number;
}

interface AssetMetaEntry {
  label: string;
  unit: string;
  category: AssetCategory;
  decimalPlaces: number;
}

const assetMeta: Record<string, AssetMetaEntry> = {
  IRT: { label: "تومان", unit: "تومان", category: "fiat", decimalPlaces: 0 },
  USD: { label: "دلار", unit: "دلار", category: "fiat", decimalPlaces: 2 },
  MAUA: {
    label: "طلای آبشده ۱۸ عیار",
    unit: "گرم",
    category: "gold",
    decimalPlaces: 4,
  },
  SEKE_BAHAR: {
    label: "سکه بهار آزادی",
    unit: "سکه",
    category: "gold",
    decimalPlaces: 4,
  },
  XAU: { label: "انس طلا", unit: "انس", category: "gold", decimalPlaces: 4 },
  BTC: { label: "بیت‌کوین", unit: "BTC", category: "crypto", decimalPlaces: 8 },
  CREDIT_MAUA: {
    label: "سقف اعتبار آبشده",
    unit: "گرم",
    category: "credit",
    decimalPlaces: 4,
  },
};

function resolveAssetMeta(asset: string): {
  meta: AssetMetaEntry;
  isCreditLedger: boolean;
} {
  const upperAsset = asset.toUpperCase();
  const isCreditLedger = upperAsset.startsWith("CREDIT_");

  const direct = assetMeta[upperAsset];
  if (direct) {
    return { meta: direct, isCreditLedger };
  }

  if (isCreditLedger) {
    const baseAsset = upperAsset.slice("CREDIT_".length);
    const baseMeta = assetMeta[baseAsset];
    if (baseMeta) {
      return {
        isCreditLedger: true,
        meta: {
          label: `سقف اعتبار ${baseMeta.label}`,
          unit: baseMeta.unit,
          category: "credit",
          decimalPlaces: baseMeta.decimalPlaces,
        },
      };
    }

    return {
      isCreditLedger: true,
      meta: {
        label: `سقف اعتبار ${baseAsset || asset}`,
        unit: baseAsset || asset,
        category: "credit",
        decimalPlaces: 4,
      },
    };
  }

  return {
    isCreditLedger: false,
    meta: {
      label: asset,
      unit: asset,
      category: "other",
      decimalPlaces: 4,
    },
  };
}

export function enrichWalletItem(raw: RawWalletDto): WalletItem {
  const { meta, isCreditLedger } = resolveAssetMeta(raw.asset);

  const balance = Number(raw.balance) || 0;
  const lockedBalance = Number(raw.lockedBalance) || 0;
  // Wallet.Core.LockBalance() already subtracts locked funds out of Balance,
  // so Balance is the available amount and total balance is Balance + LockedBalance.
  const availableBalance = balance;
  const totalBalance = balance + lockedBalance;

  return {
    asset: raw.asset,
    balance,
    lockedBalance,
    availableBalance,
    totalBalance,
    updatedAt: raw.updatedAt,
    isCreditLedger,
    persianLabel: meta.label,
    unit: meta.unit,
    category: meta.category,
    decimalPlaces: meta.decimalPlaces,
  };
}

export async function getUserWallets(
  userId: string
): Promise<{ wallets: WalletItem[]; error?: string }> {
  const baseUrl = (
    process.env.WALLET_API_URL || "http://127.0.0.1:60933"
  ).replace(/\/+$/, "");
  const apiKey = process.env.INTERNAL_API_KEY;

  const headers: Record<string, string> = {
    Accept: "application/json",
  };
  if (apiKey) {
    headers["X-Api-Key"] = apiKey;
  }

  try {
    const res = await fetch(
      `${baseUrl}/api/wallet/balances/${encodeURIComponent(userId)}`,
      {
        method: "GET",
        headers,
        cache: "no-store",
        signal: AbortSignal.timeout(8000),
      }
    );

    // 404: User has no active wallet ledger records yet
    if (res.status === 404) {
      return { wallets: [] };
    }

    if (!res.ok) {
      return {
        wallets: [],
        error: `Wallet.Api returned HTTP ${res.status}: ${res.statusText}`,
      };
    }

    const json = await res.json();
    if (!json.success || !Array.isArray(json.data)) {
      return {
        wallets: [],
        error: json.message || "Failed to parse wallet response.",
      };
    }

    const wallets: WalletItem[] = (json.data as RawWalletDto[]).map(
      enrichWalletItem
    );
    return { wallets };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : String(err);
    return {
      wallets: [],
      error: `Network error connecting to Wallet.Api: ${message}`,
    };
  }
}
