# TallaEgg Handelsplattform

[English](README.md) | [العربية](README.ar.md) | [Türkçe](README.tr.md) | [Français](README.fr.md) | [中文](README.zh-CN.md) | **Deutsch** | [हिन्दी](README.hi.md)

> Dies ist eine Übersetzung von [`README.md`](README.md). Bei Abweichungen gilt die englische Fassung.

## Überblick

Mit TallaEgg kann ein Goldhändler über Telegram mit seinen eigenen Kunden handeln.

Der Händler veröffentlicht einen zweiseitigen Kurs — den Preis, zu dem er kauft, und den Preis, zu dem er verkauft, eingegeben als ein einziges Paar `71000000-80000000`. Freigeschaltete Kunden sehen diesen Kurs und nehmen eine der beiden Seiten an. Bei jedem Handel ist der Händler die Gegenpartei; Kunden handeln niemals untereinander.

Ein neuer Nutzer, der den Bot startet, registriert sich, teilt seine Telefonnummer und wartet auf die Freischaltung durch den Händler. Bis dahin hat er keinen Zugang. Zum Handeln braucht er entweder ein Guthaben oder einen Kreditrahmen, den der Händler manuell gewährt — der Kredit ist eine Obergrenze, bis zu der sein Goldbestand ins Negative gehen darf. Zehn Gramm Kredit erlauben es einem Kunden also, zehn Gramm zu verkaufen, die er noch nicht besitzt.

Freigeschaltete Kunden können zum veröffentlichten Kurs handeln, ihren Handelsverlauf einsehen und physisch mit dem Händler abrechnen.

https://private-user-images.githubusercontent.com/45781438/530709572-98e34f7f-e778-45ec-8516-4a3372e6764b.mp4

https://private-user-images.githubusercontent.com/45781438/530709883-c2a1096b-5c32-4a05-9fe5-81720a5f2567.mp4

## Wichtigste Funktionen

- **Händler-Kursmodell (Dealer)** — der Händler veröffentlicht einen Kurs; Orders entstehen erst im Moment der Ausführung und werden sofort verbraucht, sodass keine Sicherheiten in einem ruhenden Orderbuch gebunden sind.
- Atomare Handelsabwicklung über eine transaktionale Outbox, idempotent bezüglich der Trade-ID, wobei Sicherheiten gesperrt werden, bevor eine Order zusammengeführt werden kann.
- Wallet-Domäne mit Einzahlungen, Auszahlungen, Guthabensperrung, goldbasiertem Kredit, Transaktionsverlauf und automatischer Anlage von Standard-Wallets.
- RESTful Minimal APIs für Nutzer, Wallets und Orders, die eine einheitliche `ApiResponse<T>`-Hülle zurückgeben.
- Telegram-Bot, der die Plattform-APIs nutzt und das gesamte Kunden- und Betreibererlebnis über Long Polling steuert.
- Zentrale Konfiguration (`config/appsettings.global.json`), Serilog-Logging und typisierte HTTP-Clients in allen Diensten.
- Eine Matching-Engine für Peer-to-Peer-Orderbücher ist weiterhin im Code vorhanden, **läuft aber nicht für Dealer-Symbole** — siehe [Handelsmodell](#trading-model).

## Aufbau des Repositorys

| Pfad | Beschreibung |
| --- | --- |
| `src/User` | Users-Dienst — Onboarding, Telefon/Rolle/Status, Standard-Wallets |
| `src/Wallet` | Wallet-Dienst — Guthaben, Sperrung, Abwicklung, Transaktionsverlauf |
| `src/Order` | Orders-Dienst — Kurse, Kursausführungen, Trades, Matching-Engine |
| `src/Affiliate` | Affiliate-Dienst — Einladungscodes (**derzeit nicht funktionsfähig**, siehe unten) |
| `src/TallaEgg` | Gemeinsame Core-/Application-/Infrastructure-Bibliotheken sowie eine veraltete Orchestrierungs-API |
| `tests/TallaEgg.AllServices.Tests` | Die Testsuite für die gesamte Plattform (siehe [Tests](#testing)) |
| `TelegramBot` | Telegram-Bot-Host, Handler und typisierte API-Clients |
| `config/appsettings.global.json` | Gemeinsame Konfiguration für alle Dienste — von git ignoriert ([#33](https://github.com/MohKardan/TallaEgg/issues/33)); kopieren Sie sie aus `config/appsettings.global.example.json` |
| `docs/` | Architektur, Betrieb, Prozesse, OKRs und das Geschäftskonzept |
| `governance/` | Satzung, Geschäftsordnung, Sitzungsprotokolle und `P-XXXX`-Vorschläge |
| `scripts/` | Hilfsskripte und die Werkzeuge zum Veröffentlichen/Installieren der Windows-Dienste |

<a id="trading-model"></a>
## Handelsmodell

Die Plattform unterstützt pro Symbol zwei Marktmodi, die in der Konfiguration festgelegt werden:

| Modus | Verhalten |
| --- | --- |
| **`Dealer`** (aktuell) | Der Händler veröffentlicht einen Kurs. Nimmt ein Kunde ihn an, werden beide Orders erzeugt und in einem einzigen Vorgang zusammengeführt. Die Matching-Engine im Hintergrund **überspringt diese Symbole vollständig** — andernfalls würde sie dasselbe Orderpaar erreichen, das eine Ausführung bereits zusammenführt, und aus einem Handel zwei machen. |
| `OrderBook` | Klassisches Maker/Taker-Matching über die Hintergrund-Engine. Wird derzeit nicht produktiv genutzt. |

`MAUA/IRT` (Gold / Toman), `SEKE_BAHAR/IRT` (Bahar-Azadi-Münze / Toman) und `BTC/IRT` (Bitcoin / Toman) laufen im Modus `Dealer`. Gegenpartei einer Ausführung ist, wer den Kurs veröffentlicht hat, daher muss der Händler nirgends in der Konfiguration benannt werden.

### Ein Handelssymbol hinzufügen

Ob Kunden ein Symbol handeln können, hängt von zwei voneinander unabhängigen Dingen ab, die bewusst getrennt gehalten werden:

| Was | Wo es liegt | Wie es geändert wird |
| --- | --- | --- |
| **Metadaten** — Dezimalgenauigkeit, Mindest-/Höchstmenge, persischer Anzeigename, welches nerkh.io-/brsapi.ir-Instrument den Preis liefert | `Symbols:{Base}/{Quote}` in `appsettings.global.json` (siehe den dort bereits vorhandenen Block für die drei oben genannten Symbole) | Datei bearbeiten, betroffene(n) Dienst(e) neu starten. Keine Codeänderung, kein Neubau. |
| **Aktiv oder nicht** — in der Symbolauswahl des Kunden sichtbar, für automatische Kurse zulässig, für manuelle Kurse nutzbar | Eine Datenbankzeile pro Symbol (`SymbolSettings`, neben `AutoQuoteSettings`) | Ein Bot-Befehl, sofort, ohne Neustart: `نماد فعال [سکه\|بیت]` / `نماد غیرفعال [...]`. Ohne Schlüsselwort ist MAUA/IRT gemeint. |

Ein Symbol, das der Standardform entspricht — in Toman notiert und über nerkh.io und/oder brsapi.ir bepreist, genauso wie Gold/Münze/Bitcoin bereits — braucht **nur einen Konfigurationsblock** und anschließend einen Administrator, der es aktiviert. `TallaEgg.Core.CurrenciesConstant` enthält die drei oben genannten Symbole als einkompilierte Standardwerte (sodass die Testsuite und ein frischer Klon keinerlei Konfigurationsdatei benötigen); ein Konfigurationsblock für einen *neuen* Schlüssel fügt einen vierten Eintrag hinzu, und ein Block für einen *vorhandenen* Schlüssel überschreibt nur die Felder, die er setzt.

`Matching:MarketModes` (oben) ist wiederum davon getrennt — es entscheidet zwischen `Dealer` und `OrderBook` und braucht weiterhin einen eigenen Eintrag pro Symbol.

Ein Symbol, dessen Preis aus einer Quelle stammt, die weder nerkh.io noch brsapi.ir abdeckt, braucht nach wie vor eine neue Klasse, die `Orders.Core.IReferencePriceProvider` implementiert — das ist der eine Teil, der unvermeidlich Code ist, weil es sich um eine neue externe Integration handelt und nicht um eine neue Symboldefinition.

## Technologie-Stack

- .NET 9.0 mit C# 12, Minimal APIs und Hintergrunddiensten.
- Entity Framework Core 9 mit dem SQL-Server-Provider.
- Serilog für strukturiertes Logging in die Konsole und in rotierende Dateien.
- Telegram.Bot über Long Polling — **keine eingehenden Ports erforderlich**.
- Typisierte `HttpClient`-Wrapper für Aufrufe zwischen Diensten.

## Voraussetzungen

- .NET SDK 9.0.
- **SQL Server Express**, vom Host aus erreichbar (eine benannte Instanz `SQLEXPRESS`, Windows-Authentifizierung). Nicht LocalDB — LocalDB ist eine benutzerbezogene, bei Bedarf gestartete Instanz, die auf einem Server nicht existiert, und genau das hat die Bereitstellung vor [#68](https://github.com/MohKardan/TallaEgg/issues/68) zum Scheitern gebracht. Express ist dieselbe Engine, die eine echte (Einzelrechner-)Bereitstellung verwendet, nur lokal installiert.
- Ein Telegram-Bot-Token von [@BotFather](https://t.me/BotFather).
- Ihre eigene numerische Telegram-Nutzer-ID (fragen Sie [@userinfobot](https://t.me/userinfobot)) — dadurch werden Sie auf einer frischen Datenbank zum Betreiber des Händlers.

## Konfiguration

Jeder Dienst lädt `config/appsettings.global.json` und übernimmt dann den Abschnitt unter `Services:`, der seinem eigenen Assembly-Namen entspricht. Es gibt keine dienstspezifische `appsettings.json`, die gepflegt werden müsste.

> ⚠️ **Alte Bot-Tokens und der alte gemeinsame API-Schlüssel sind in diesem Repository noch lesbar** — in der git-Historie, weil diese Datei früher versioniert war, und in einer versionierten Quelldatei: der Registrierung von `TelegramLoggerService` in `TallaEgg.TelegramBot.Infrastructure/Program.cs`, die keinen eigenen Konfigurationsschlüssel hat. **Jeder einzelne davon wurde unter [#33](https://github.com/MohKardan/TallaEgg/issues/33) ausgetauscht und ist ungültig — melden Sie sie nicht als aktives Leck.** Neue Leser entdecken sie bei jedem Durchgang wieder und lösen einen Vorfall aus; es ist jedes Mal ein Fehlalarm. Die Historie wird bewusst **nicht** umgeschrieben — diese Entscheidung und ihre Begründung stehen in [#105](https://github.com/MohKardan/TallaEgg/issues/105). Die Konfigurationsdatei selbst wird inzwischen von git ignoriert; fügen Sie sie, oder ein neues Geheimnis, nicht wieder der Versionskontrolle hinzu.

Erstellen Sie Ihre eigene Kopie anhand der folgenden Vorlage.

```json
{
  "ConnectionStrings": {
    "UsersDb": "Server=localhost\\SQLEXPRESS;Database=TallaEggUsers;Trusted_Connection=True;TrustServerCertificate=True;",
    "WalletDb": "Server=localhost\\SQLEXPRESS;Database=TallaEggWallet;Trusted_Connection=True;TrustServerCertificate=True;",
    "OrdersDb": "Server=localhost\\SQLEXPRESS;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
    "AffiliateDb": "Server=localhost\\SQLEXPRESS;Database=TallaEggAffiliate;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Services": {
    "Users.Api": {
      "Urls": [ "http://localhost:5136" ],
      "WalletApiUrl": "http://localhost:60933/"
    },
    "Wallet.Api": {
      "Urls": [ "http://localhost:60933" ]
    },
    "Orders.Api": {
      "Urls": [ "http://localhost:5140" ],
      "UsersApiUrl": "http://localhost:5136/api",
      "WalletApiUrl": "http://localhost:60933/api",
      "Matching": {
        "RequireMarketMakerCounterparty": true,
        "MarketModes": { "MAUA/IRT": "Dealer", "SEKE_BAHAR/IRT": "Dealer", "BTC/IRT": "Dealer" }
      }
    },
    "Affiliate.Api": {
      "Urls": [ "http://localhost:60812" ]
    },
    "TallaEgg.TelegramBot.Infrastructure": {
      "Urls": [ "http://localhost:57546" ],
      "OrderApiUrl": "http://localhost:5140/api",
      "UsersApiUrl": "http://localhost:5136/api",
      "AffiliateApiUrl": "http://localhost:60812/api",
      "PricesApiUrl": "http://localhost:5140/api",
      "WalletApiUrl": "http://localhost:60933/api",
      "BotSettings": {
        "RequireReferralCode": false,
        "DefaultReferralCode": "admin",
        "OwnerTelegramIds": [ 123456789 ]
      },
      "TelegramBotToken": "<token from BotFather>"
    }
  }
}
```

### Die entscheidenden Einstellungen

| Einstellung | Warum sie wichtig ist |
| --- | --- |
| `BotSettings:OwnerTelegramIds` | Das Einzige, was auf einer leeren Datenbank überhaupt jemanden hereinlässt. Ein konfigurierter Eigentümer wird bei der Registrierung automatisch freigeschaltet und erhält die Rolle `Admin`. Tragen Sie hier **Ihre eigene** Telegram-ID ein. |
| `BotSettings:DefaultReferralCode` | Muss `admin` sein — der Code der Administratorzeile, die `Users.Api` anlegt. Die Registrierung lehnt jeden Code ab, der keinem Nutzer gehört; eine Abweichung hier bedeutet also, dass **sich niemand registrieren kann**. |
| `Matching:MarketModes` | Ohne ein auf `"Dealer"` gesetztes Symbol (z. B. `"MAUA/IRT": "Dealer"`) fällt es auf `OrderBook` zurück, und jede Kursausführung für dieses Symbol wird abgelehnt. |
| `TelegramBotToken` | Wird normalerweise aus dieser Datei gelesen, und der Bot startet ohne ihn nicht. Es gibt keinen Rückgriff auf `TELEGRAM_BOT_TOKEN` — dieser Name wird nicht ausgewertet. Der Schlüssel lässt sich dennoch pro Prozess überschreiben, weil die Konfigurationskette des Bots mit Umgebungsvariablen und danach der Kommandozeile endet (#181); `TelegramBotToken=...` in der Umgebung oder `--TelegramBotToken=...` auf der Kommandozeile haben also beide Vorrang vor der Datei. |

### Ports und Bind-Adressen

Jeder `Urls`-Eintrag oben ist absichtlich einfaches HTTP auf Loopback (`localhost`) — siehe [#69](https://github.com/MohKardan/TallaEgg/issues/69):

- Der Bot erreicht Telegram über **Long Polling**; er baut die Verbindung selbst auf, Telegram verbindet sich nie zu ihm.
- Die vier echten APIs werden ausschließlich vom Bot auf demselben Host aufgerufen.
- Daher **muss kein eingehender Port offen sein**, und auch keine HTTPS-Bind-Adresse ist nötig — ein Eintrag `https://localhost:...` erfordert ein Entwicklerzertifikat, das auf einem Server nicht existiert, und zwischen einem aus dem Internet erreichbaren Port und der Datenbank stünde nur der oben genannte gemeinsame API-Schlüssel. Die Bindung an Loopback ist hier die eigentliche Sicherheitsmaßnahme, kein Platzhalter, der ersetzt werden soll.

| Dienst | Port | Zweck |
| --- | --- | --- |
| Users.Api | 5136 | Registrierung, Rollen, Anlage von Wallets |
| Wallet.Api | 60933 | Guthaben, Abwicklung |
| Orders.Api | 5140 | Kurse, Trades, Matching |
| Affiliate.Api | 60812 | Nicht bereitgestellt — siehe [Datenbankeinrichtung](#database-setup) |
| TallaEgg.TelegramBot.Infrastructure | 57546 | Konfiguriert, aber nichts lauscht darauf — der Bot ist ein einfacher generischer Host ohne Webserver und erreicht Telegram über Long Polling |
| TallaEgg.Api | 5135 | Nicht bereitgestellt — veraltet, nichts ruft sie auf |

Prüfen Sie auf einem echten Server, dass keiner dieser Ports in `netstat`/`ss` auf einer öffentlichen Schnittstelle erscheint — nur auf `127.0.0.1`.

### Gemeinsamer API-Schlüssel

Wallet.Api, Users.Api, Orders.Api und Affiliate.Api authentifizieren Aufrufe zwischen Diensten mit einem gemeinsamen Schlüssel, der im Header `X-API-Key` gesendet wird (`TallaEgg.Core.APIKeyConstant`). Er wird aus der Umgebungsvariablen `TALLAEGG_API_KEY` gelesen, nicht aus einer versionierten Datei.

- **Entwicklung**: Keiner der vier Dienste erzwingt ihn — die API-Schlüssel-Authentifizierung wird nur bei `ASPNETCORE_ENVIRONMENT=Production` eingebunden. Lassen Sie die Variable lokal ungesetzt.
- **Produktion**: Setzen Sie `TALLAEGG_API_KEY`, bevor Sie einen Dienst starten; jeder Dienst bricht beim Start mit einer Ausnahme ab — noch bevor er seinen Port bindet —, wenn sie fehlt.

> **`ASPNETCORE_ENVIRONMENT` muss auf einem Server ausdrücklich gesetzt werden.** Lokal meldet `dotnet run` wegen `launchSettings.json` immer `Development` — diese Datei wird von einer veröffentlichten Bereitstellung nicht verwendet, sodass ein Server außerhalb von `dotnet run` standardmäßig `Production` ist, sobald sie gesetzt (oder implizit gelassen) wird. Bis [#69](https://github.com/MohKardan/TallaEgg/issues/69) war der `Production`-Codepfad — API-Schlüssel-Authentifizierung, keine Ausnahme für die Swagger-Weiterleitung — nie tatsächlich ausgeführt worden.

<a id="database-setup"></a>
## Datenbankeinrichtung

Jede API ruft beim Start `Database.MigrateAsync()` auf, sodass das Schema beim ersten Start angelegt wird. Für Users, Wallet und Orders ist kein manueller Schritt nötig.

Um Migrationen vorab anzuwenden:

```
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/User/Users.Api/Users.Api.csproj
dotnet ef database update --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet ef database update --project src/Order/Orders.Api/Orders.Api.csproj
```

`Users.Api` legt eine Administratorzeile an (`5564f136-b9fb-4719-b4dc-b0833fa24761`), deren einziger Zweck es ist, den initialen Einladungscode zu besitzen. Sie hat keine Telegram-ID, und man kann sich nicht als sie anmelden.

> **Affiliate hat keine Migrationen.** `Affiliate.Api` ruft `MigrateAsync()` auf, liefert aber keine einzige Migrationsdatei mit; der Dienst startet daher sauber und scheitert dann bei jeder Anfrage mit `Invalid object name 'Invitations'`. Derzeit ruft ihn nichts auf — der einzige Einladungsaufruf des Bots ist auskommentiert —, daher kann er in einer Bereitstellung weggelassen werden.

## Erster Start

Auf einer leeren Datenbank ist dies die gesamte Einrichtung. Es muss kein SQL ausgeführt und keine ID zwischen Dateien kopiert werden.

1. Tragen Sie Ihre Telegram-ID in `BotSettings:OwnerTelegramIds` ein und starten Sie die Dienste.
2. Senden Sie `/start` an den Bot und teilen Sie Ihre Telefonnummer, wenn Sie dazu aufgefordert werden.
   → Sie werden automatisch freigeschaltet und erhalten die Rolle `Admin`.
3. Veröffentlichen Sie einen Kurs, indem Sie Kauf- und Verkaufspreis als ein Paar senden, z. B. `79000000-79500000`.
4. Lassen Sie einen Kunden `/start` senden und seine Nummer teilen, und schalten Sie ihn dann frei: `ت <seine Telefonnummer>`.
5. Gewähren Sie ihm Kredit: `ش <seine Telefonnummer> 10 طلا`.
6. Er kann jetzt zum veröffentlichten Kurs handeln.

### Betreiberbefehle

Preise gelten pro Mesghal; Goldmengen sind in Gramm. Zahlen können mit persischen oder lateinischen Ziffern eingegeben werden.

| Befehl | Wirkung |
| --- | --- |
| `<buy>-<sell>` | Einen Kurs veröffentlichen, z. B. `79000000-79500000` |
| `ت <phone>` | Ein Konto freischalten |
| `ر <phone>` | Ein Konto ablehnen |
| `ن <phone> <role>` | Eine Rolle ändern — `کاربر عادی`, `حسابدار`, `مدیر`, `مدیر ارشد` |
| `ش <phone> <amount> <asset>` | Einem Konto gutschreiben, z. B. `ش 09121234567 500000 تومان` |
| `د <phone> <amount> <asset>` | Ein Konto belasten |
| `م <phone>` | Guthaben anzeigen |
| `س <phone>` | Die offenen Orders eines Nutzers anzeigen |
| `ک [search]` | Nutzer auflisten |

## Lokal ausführen

Einmal bauen, dann jeden Dienst in einem eigenen Terminal starten. Ein Build bei laufenden Diensten scheitert an gesperrten DLLs.

```
dotnet build TallaEgg.sln

dotnet run --no-build --project src/User/Users.Api/Users.Api.csproj
dotnet run --no-build --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --no-build --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

`Affiliate.Api` wird nicht benötigt (siehe oben). `src/TallaEgg/TallaEgg.Api` ist eine veraltete Orchestrierungs-API, die nichts aufruft.

Die Swagger-Oberfläche liegt unter `/api-docs` bei Users, Wallet und Orders — zum Beispiel `http://localhost:5136/api-docs`. Sie wird **nur bei `ASPNETCORE_ENVIRONMENT=Development`** eingebunden und existiert daher auf einem bereitgestellten Server nicht; `dotnet run` setzt diesen Wert aus `launchSettings.json`, lokal ist sie also standardmäßig vorhanden. Der Bot stellt überhaupt keine HTTP-Endpunkte bereit.

<a id="production-deployment"></a>
## Produktionsbereitstellung

Auf einem Server laufen die vier bereitgestellten Dienste (nicht Affiliate.Api oder TallaEgg.Api — siehe oben) als native Windows-Dienste statt als ein Terminal pro Prozess, sodass sie nach einem Absturz oder Neustart automatisch wieder starten. Die einmalige Einrichtung und die Installationsskripte unter `scripts/windows-services/` sind in [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) beschrieben — Issue #70.

## Die Dienste im Überblick

- **Users.Api** — Registrierung mit Einladungscodes, Telefonnummernänderungen, Rollen- und Statusverwaltung, Anlage von Standard-Wallets, Suche nach Telegram-ID, Telefonnummer oder Rolle.
- **Wallet.Api** — Guthaben, Einzahlungen, Auszahlungen, Sperren/Entsperren, atomare Handelsabwicklung, Transaktionsverlauf.
- **Orders.Api** — Veröffentlichung und Verlauf von Kursen, Kursausführungen, Handelsverlauf, bester Geld-/Briefkurs und die (für Dealer-Symbole ruhende) Matching-Engine.
- **Telegram-Bot** — Long-Polling-Host, typisierte API-Clients, Handelsbenachrichtigungen und die oben beschriebenen Betreiberbefehle.
- **Affiliate.Api** — Einladungscodes. Vorhanden, aber nicht funktionsfähig; siehe den Hinweis unter Datenbankeinrichtung.

<a id="testing"></a>
## Tests

```
dotnet test TallaEgg.sln
```

**`tests/TallaEgg.AllServices.Tests` ist das einzige Testprojekt der Solution** und enthält die Suite für die gesamte Plattform — Wallet, Orders, Matching, Kursausführungen, Bot-Handler und Formatierung. Neue Tests gehören hierher, unabhängig davon, welchen Dienst sie abdecken.

Der Name ist bewusst eindeutig. Dieses Projekt hieß bis #117 `src/Wallet/Wallet.Tests`; zu diesem Zeitpunkt betrafen nur noch acht seiner sechsundfünfzig Dateien die Wallet — so irreführend, dass ein Audit den Bot als ungetestet meldete, weil seine Tests nicht in einem nach dem Bot benannten Ordner lagen.

Die CI führt `dotnet test TallaEgg.sln` aus, statt dieses Projekt über seinen Pfad zu benennen; ein künftiges Testprojekt wird also allein dadurch erfasst, dass es zur Solution hinzugefügt wird, und an nichts anderes muss gedacht werden.

## Logging

Jeder Dienst schreibt in die Konsole und in eine rotierende Datei in einem Verzeichnis `logs/` **neben seiner eigenen Binärdatei**, nicht in das Verzeichnis, aus dem er gestartet wurde.

Unter `dotnet run` ist das die Build-Ausgabe — `src/Order/Orders.Api/bin/Debug/net9.0/logs/orders-api-<date>.log`. Auf einem bereitgestellten Rechner ist es der Veröffentlichungsordner — `C:\TallaEgg\publish\Orders.Api\logs\orders-api-<date>.log`. Für den Bereitstellungsfall, der bis [#211](https://github.com/MohKardan/TallaEgg/issues/211) nach `C:\Windows\System32\logs\` schrieb, siehe [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md).

## Bereitstellung

Long Polling bedeutet, dass der Bot **keine eingehenden Ports** braucht. Die Bereitstellungsarbeit von KR1 ([#68](https://github.com/MohKardan/TallaEgg/issues/68) Datenbank, [#69](https://github.com/MohKardan/TallaEgg/issues/69) Produktions-URLs, [#70](https://github.com/MohKardan/TallaEgg/issues/70) Prozessüberwachung und [#71](https://github.com/MohKardan/TallaEgg/issues/71) CI) ist abgeschlossen — die tatsächlichen Schritte stehen oben unter [Produktionsbereitstellung](#production-deployment) und in [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md). `Production` wurde lokal gegen SQL Server Express ausgeführt und verifiziert, nicht nur Development — der frühere Hinweis an dieser Stelle, dass dieser Pfad nie ausgeführt worden sei, trifft nicht mehr zu.

Verwenden Sie `scripts/windows-services/` zum Veröffentlichen und Installieren der Dienste — es ist inzwischen das einzige Veröffentlichungswerkzeug im Repository. Ein älteres `publish-all.ps1` im Stammverzeichnis und ein Ordner `publishes/` stammten aus der Zeit vor #69/#70 und wurden entfernt: Sie veröffentlichten fünf statt vier Dienste (einschließlich der beiden, die laut #69 nicht bereitgestellt werden), verwiesen noch auf die HTTPS-Bind-Adressen, die #69 entfernt hat, und setzten einen manuellen Start per `dotnet *.dll` statt eines überwachten Dienstes voraus.
