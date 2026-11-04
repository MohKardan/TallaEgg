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
  updatedAt: string;
  isCreditLedger: boolean;
  persianLabel: string;
  unit: string;
}

const assetMeta: Record<string, { label: string; unit: string }> = {
  IRT: { label: "تومان", unit: "تومان" },
  MAUA: { label: "طلای آبشده ۱۸ عیار", unit: "گرم" },
  CREDIT_MAUA: { label: "سقف اعتبار آبشده", unit: "گرم" },
  SEKE_BAHAR: { label: "سکه بهار آزادی", unit: "سکه" },
  BTC: { label: "بیت‌کوین", unit: "BTC" },
  "XAU/USD": { label: "انس طلا", unit: "انس" },
};

export function enrichWalletItem(raw: RawWalletDto): WalletItem {
  const isCredit = raw.asset.toUpperCase().startsWith("CREDIT_");
  const meta = assetMeta[raw.asset.toUpperCase()] || {
    label: raw.asset,
    unit: raw.asset,
  };

  const balance = Number(raw.balance) || 0;
  const lockedBalance = Number(raw.lockedBalance) || 0;
  // Available is balance minus locked
  const availableBalance = balance - lockedBalance;

  return {
    asset: raw.asset,
    balance,
    lockedBalance,
    availableBalance,
    updatedAt: raw.updatedAt,
    isCreditLedger: isCredit,
    persianLabel: meta.label,
    unit: meta.unit,
  };
}

export async function getUserWallets(
  userId: string
): Promise<{ wallets: WalletItem[]; error?: string }> {
  const baseUrl = process.env.WALLET_API_URL || "http://127.0.0.1:60933";
  const apiKey = process.env.INTERNAL_API_KEY;

  const headers: Record<string, string> = {
    Accept: "application/json",
  };
  if (apiKey) {
    headers["X-Api-Key"] = apiKey;
  }

  try {
    const res = await fetch(`${baseUrl}/api/wallet/balances/${userId}`, {
      method: "GET",
      headers,
      cache: "no-store",
    });

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
  } catch (err: any) {
    return {
      wallets: [],
      error: `Network error connecting to Wallet.Api: ${err.message}`,
    };
  }
}
