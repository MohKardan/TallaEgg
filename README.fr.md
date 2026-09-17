# Plateforme de négoce TallaEgg

[English](README.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Türkçe](README.tr.md) | **Français** | [中文](README.zh-CN.md) | [Deutsch](README.de.md) | [हिन्दी](README.hi.md) | [Русский](README.ru.md)

> Ceci est une traduction de [`README.md`](README.md). En cas de divergence, la version anglaise fait foi.

## Présentation

TallaEgg permet à une bijouterie-comptoir d'or de négocier avec ses propres clients via Telegram.

La boutique publie une cotation à double sens — le prix auquel elle achète et le prix auquel elle vend, saisis comme une seule paire `71000000-80000000`. Les clients approuvés voient cette cotation et prennent l'un ou l'autre côté. La boutique est la contrepartie de chaque transaction ; les clients ne négocient jamais entre eux.

Un nouvel utilisateur qui démarre le bot s'inscrit, partage son numéro de téléphone et attend que la boutique l'approuve. D'ici là, il n'a aucun accès. Pour négocier, il lui faut soit un solde, soit un crédit, que la boutique accorde manuellement — le crédit est un plafond jusqu'auquel son solde d'or peut devenir négatif ; dix grammes de crédit permettent donc à un client de vendre dix grammes qu'il ne détient pas encore.

Les clients approuvés peuvent négocier au prix coté, consulter l'historique de leurs transactions et effectuer un règlement physique avec la boutique.

https://private-user-images.githubusercontent.com/45781438/530709572-98e34f7f-e778-45ec-8516-4a3372e6764b.mp4

https://private-user-images.githubusercontent.com/45781438/530709883-c2a1096b-5c32-4a05-9fe5-81720a5f2567.mp4

## Fonctionnalités principales

- **Modèle de cotation par le teneur de marché (Dealer)** — la boutique publie une cotation ; les ordres ne sont créés qu'au moment de l'exécution et consommés immédiatement, si bien qu'aucune garantie ne reste bloquée dans un carnet d'ordres en attente.
- Règlement atomique des transactions via une outbox transactionnelle, idempotent sur l'identifiant de transaction, avec blocage des garanties avant qu'un ordre ne devienne appariable.
- Domaine portefeuille avec dépôts, retraits, blocage de solde, crédit libellé en or, historique des transactions et création automatique des portefeuilles par défaut.
- API minimales RESTful pour les utilisateurs, les portefeuilles et les ordres, renvoyant une enveloppe unifiée `ApiResponse<T>`.
- Bot Telegram qui consomme les API de la plateforme et pilote toute l'expérience client et opérateur par long polling.
- Configuration centralisée (`config/appsettings.global.json`), journalisation Serilog et clients HTTP typés dans tous les services.
- Un moteur d'appariement pour carnets d'ordres pair-à-pair subsiste dans le code mais **ne fonctionne pas pour les symboles Dealer** — voir [Modèle de négoce](#trading-model).

## Organisation du dépôt

| Chemin | Description |
| --- | --- |
| `src/User` | Service Users — inscription, téléphone/rôle/statut, portefeuilles par défaut |
| `src/Wallet` | Service Wallet — soldes, blocage, règlement, historique des transactions |
| `src/Order` | Service Orders — cotations, exécutions de cotations, transactions, moteur d'appariement |
| `src/Affiliate` | Service Affiliate — codes d'invitation (**actuellement non fonctionnel**, voir ci-dessous) |
| `src/TallaEgg` | Bibliothèques partagées core/application/infrastructure et une ancienne API d'orchestration |
| `tests/TallaEgg.AllServices.Tests` | La suite de tests de toute la plateforme (voir [Tests](#testing)) |
| `TelegramBot` | Hôte du bot Telegram, gestionnaires et clients API typés |
| `config/appsettings.global.json` | Configuration partagée par tous les services — ignorée par git ([#33](https://github.com/MohKardan/TallaEgg/issues/33)) ; copiez-la depuis `config/appsettings.global.example.json` |
| `docs/` | Architecture, exploitation, processus, OKR et proposition commerciale |
| `governance/` | Charte, statuts, comptes rendus de réunion et propositions `P-XXXX` |
| `scripts/` | Scripts utilitaires et outils de publication/installation des services Windows |

<a id="trading-model"></a>
## Modèle de négoce

La plateforme prend en charge deux modes de marché par symbole, définis dans la configuration :

| Mode | Comportement |
| --- | --- |
| **`Dealer`** (actuel) | La boutique publie une cotation. Lorsqu'un client l'accepte, les deux ordres sont créés et appariés en une seule opération. Le moteur d'appariement en arrière-plan **ignore totalement ces symboles** — sinon il atteindrait la même paire d'ordres qu'une exécution est déjà en train d'apparier et produirait deux transactions à partir d'une seule. |
| `OrderBook` | Appariement classique maker/taker par le moteur en arrière-plan. Non utilisé en production aujourd'hui. |

`MAUA/IRT` (or / toman), `SEKE_BAHAR/IRT` (pièce Bahar Azadi / toman) et `BTC/IRT` (Bitcoin / toman) fonctionnent en mode `Dealer`. La contrepartie d'une exécution est celui qui a publié la cotation ; rien n'a donc besoin de nommer la boutique dans la configuration.

### Ajouter un symbole de négoce

Deux éléments indépendants déterminent si les clients peuvent négocier un symbole, et ils sont délibérément séparés :

| Quoi | Où cela se trouve | Comment le modifier |
| --- | --- | --- |
| **Métadonnées** — précision décimale, quantité min/max, nom d'affichage en persan, instrument nerkh.io/brsapi.ir qui fournit le prix | `Symbols:{Base}/{Quote}` dans `appsettings.global.json` (voir le bloc déjà présent pour les trois symboles ci-dessus) | Modifier le fichier, redémarrer le ou les services concernés. Aucun changement de code, aucune recompilation. |
| **Actif ou non** — affiché dans le sélecteur de symboles du client, éligible à la cotation automatique, utilisable pour une cotation manuelle | Une ligne de base de données par symbole (`SymbolSettings`, à côté de `AutoQuoteSettings`) | Une commande du bot, immédiatement, sans redémarrage : `نماد فعال [سکه\|بیت]` / `نماد غیرفعال [...]`. Sans mot-clé, il s'agit de MAUA/IRT. |

Un symbole de forme standard — libellé en toman, dont le prix provient de nerkh.io et/ou brsapi.ir de la même manière que l'or, la pièce et le Bitcoin — n'a besoin **que d'un bloc de configuration**, puis d'un administrateur qui l'active. `TallaEgg.Core.CurrenciesConstant` embarque les trois symboles ci-dessus comme valeurs par défaut compilées (ainsi, la suite de tests et un clone neuf n'ont besoin d'aucun fichier de configuration) ; un bloc de configuration pour une *nouvelle* clé ajoute une quatrième entrée, et un bloc pour une clé *existante* ne remplace que les champs qu'il définit.

`Matching:MarketModes` (ci-dessus) est encore distinct — il choisit entre `Dealer` et `OrderBook` et nécessite toujours sa propre entrée par symbole.

Un symbole dont le prix provient d'une source que ni nerkh.io ni brsapi.ir ne couvrent nécessite toujours une nouvelle classe implémentant `Orders.Core.IReferencePriceProvider` — c'est la seule partie qui relève inévitablement du code, car il s'agit d'une nouvelle intégration externe et non d'une nouvelle définition de symbole.

## Pile technique

- .NET 9.0 avec C# 12, API minimales et services d'arrière-plan.
- Entity Framework Core 9 avec le fournisseur SQL Server.
- Serilog pour une journalisation structurée vers la console et des fichiers tournants.
- Telegram.Bot en long polling — **aucun port entrant n'est nécessaire**.
- Enveloppes `HttpClient` typées pour les appels entre services.

## Prérequis

- SDK .NET 9.0.
- **SQL Server Express** accessible depuis l'hôte (une instance nommée `SQLEXPRESS`, authentification Windows). Pas LocalDB — LocalDB est une instance par utilisateur, lancée à la demande, qui n'existe pas sur un serveur, et c'est exactement ce qui faisait échouer le déploiement avant [#68](https://github.com/MohKardan/TallaEgg/issues/68). Express est le même moteur que celui d'un vrai déploiement (sur une seule machine), simplement installé en local.
- Un jeton de bot Telegram obtenu auprès de [@BotFather](https://t.me/BotFather).
- Votre propre identifiant numérique Telegram (demandez-le à [@userinfobot](https://t.me/userinfobot)) — c'est ce qui fait de vous l'opérateur de la boutique sur une base de données neuve.

## Configuration

Chaque service charge `config/appsettings.global.json`, puis aplatit la section située sous `Services:` qui correspond à son propre nom d'assembly. Il n'existe aucun `appsettings.json` par service à maintenir.

> ⚠️ **D'anciens jetons de bot et l'ancienne clé API partagée sont encore lisibles dans l'historique git de ce dépôt**, car ce fichier était autrefois suivi. Plus aucun jeton ne figure dans les fichiers source suivis. **Considérez tout ce que vous trouvez dans l'historique comme compromis, et ne vous fiez pas à un document qui affirme qu'il a été renouvelé** : vérifiez-le, et révoquez-le via BotFather (ou renouvelez la clé) s'il fonctionne encore. L'historique n'est délibérément **pas** réécrit — le renouvellement est le remède ; cette décision et sa justification figurent dans [#105](https://github.com/MohKardan/TallaEgg/issues/105). Le fichier de configuration lui-même est désormais ignoré par git ; ne le rajoutez pas, ni aucun nouveau secret, au contrôle de version.

Créez votre propre copie à partir du modèle ci-dessous.

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

### Les paramètres qui comptent

| Paramètre | Pourquoi il compte |
| --- | --- |
| `BotSettings:OwnerTelegramIds` | La seule chose qui permet à quiconque d'entrer sur une base de données vide. Un propriétaire configuré est approuvé et reçoit automatiquement le rôle `Admin` lors de son inscription. Indiquez ici **votre propre** identifiant Telegram. |
| `BotSettings:DefaultReferralCode` | Doit valoir `admin` — le code porté par la ligne administrateur que `Users.Api` crée au démarrage. L'inscription rejette tout code n'appartenant à aucun utilisateur ; une discordance ici signifie donc que **personne ne peut s'inscrire**. |
| `Matching:MarketModes` | Sans symbole réglé sur `"Dealer"` (par ex. `"MAUA/IRT": "Dealer"`), le mode retombe sur `OrderBook` et toute exécution de cotation pour ce symbole est refusée. |
| `TelegramBotToken` | Normalement lu depuis ce fichier, et le bot refuse de démarrer sans lui. Il n'existe aucun repli sur `TELEGRAM_BOT_TOKEN` — ce nom n'est pas consulté. La clé peut néanmoins être surchargée par processus, car la chaîne de configuration du bot se termine par les variables d'environnement puis la ligne de commande (#181) ; `TelegramBotToken=...` dans l'environnement ou `--TelegramBotToken=...` en ligne de commande l'emportent donc tous deux sur le fichier. |

### Ports et adresses d'écoute

Chaque entrée `Urls` ci-dessus est volontairement en HTTP simple sur la boucle locale (`localhost`) — voir [#69](https://github.com/MohKardan/TallaEgg/issues/69) :

- Le bot joint Telegram par **long polling** ; c'est lui qui initie la connexion, Telegram ne se connecte jamais à lui.
- Les quatre véritables API ne sont appelées que par le bot, sur le même hôte.
- Ainsi, **aucun port entrant n'a besoin d'être ouvert**, et aucune adresse d'écoute HTTPS n'est nécessaire non plus — une entrée `https://localhost:...` exige un certificat de développement qui n'existera pas sur un serveur, et la seule chose entre un port exposé à Internet et la base de données serait la clé API partagée mentionnée plus haut. L'écoute sur la boucle locale est ici le véritable contrôle de sécurité, et non un paramètre provisoire à remplacer.

| Service | Port | Rôle |
| --- | --- | --- |
| Users.Api | 5136 | Inscription, rôles, création des portefeuilles |
| Wallet.Api | 60933 | Soldes, règlement |
| Orders.Api | 5140 | Cotations, transactions, appariement |
| Affiliate.Api | 60812 | Non déployé — voir [Mise en place de la base de données](#database-setup) |
| TallaEgg.TelegramBot.Infrastructure | 57546 | Configuré, mais rien n'y écoute — le bot est un simple hôte générique sans serveur web et joint Telegram par long polling |
| TallaEgg.Api | 5135 | Non déployé — ancien, rien ne l'appelle |

Sur un vrai serveur, vérifiez qu'aucun de ces ports n'apparaît dans `netstat`/`ss` sur une interface publique — uniquement sur `127.0.0.1`.

### Clé API partagée

Wallet.Api, Users.Api, Orders.Api et Affiliate.Api authentifient les appels entre services à l'aide d'une clé partagée envoyée dans l'en-tête `X-API-Key` (`TallaEgg.Core.APIKeyConstant`). Elle est lue depuis la variable d'environnement `TALLAEGG_API_KEY`, et non depuis un fichier suivi.

- **Développement** : aucun des quatre services ne l'impose — l'authentification par clé API n'est activée que lorsque `ASPNETCORE_ENVIRONMENT=Production`. Laissez la variable non définie en local.
- **Production** : définissez `TALLAEGG_API_KEY` avant de démarrer un service ; chacun lève une exception au démarrage — avant même d'ouvrir son port — si elle est absente.

> **`ASPNETCORE_ENVIRONMENT` doit être défini explicitement sur un serveur.** En local, `dotnet run` indique toujours `Development` à cause de `launchSettings.json` — ce fichier n'est pas utilisé par un déploiement publié ; un serveur hors de `dotnet run` passe donc par défaut en `Production` dès qu'elle est définie (ou laissée implicite). Jusqu'à [#69](https://github.com/MohKardan/TallaEgg/issues/69), le chemin de code `Production` — authentification par clé API, pas d'exemption de redirection pour Swagger — n'avait jamais réellement été exécuté.

<a id="database-setup"></a>
## Mise en place de la base de données

Chaque API appelle `Database.MigrateAsync()` au démarrage ; le schéma est donc créé lors de la première exécution. Aucune étape manuelle n'est nécessaire pour Users, Wallet ou Orders.

Pour appliquer les migrations à l'avance :

```
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/User/Users.Api/Users.Api.csproj
dotnet ef database update --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet ef database update --project src/Order/Orders.Api/Orders.Api.csproj
```

`Users.Api` crée une ligne administrateur (`5564f136-b9fb-4719-b4dc-b0833fa24761`) dont le seul but est de détenir le code d'invitation initial. Elle n'a pas d'identifiant Telegram et il est impossible de se connecter en tant qu'elle.

> **Affiliate n'a pas de migrations.** `Affiliate.Api` appelle `MigrateAsync()` mais ne livre aucun fichier de migration ; il démarre donc proprement, puis échoue à chaque requête avec `Invalid object name 'Invitations'`. Rien ne l'appelle actuellement — le seul appel d'invitation du bot est commenté — il peut donc être omis d'un déploiement.

## Premier démarrage

Sur une base de données vide, voici toute la mise en place. Il n'y a aucun SQL à exécuter ni aucun identifiant à copier d'un fichier à l'autre.

1. Indiquez votre identifiant Telegram dans `BotSettings:OwnerTelegramIds` et démarrez les services.
2. Envoyez `/start` au bot, puis partagez votre numéro de téléphone lorsque cela vous est demandé.
   → Vous êtes approuvé et recevez automatiquement le rôle `Admin`.
3. Publiez une cotation en envoyant le prix d'achat et le prix de vente sous forme d'une paire, par ex. `79000000-79500000`.
4. Demandez à un client d'envoyer `/start` et de partager son numéro, puis approuvez-le : `ت <son téléphone>`.
5. Accordez-lui un crédit : `ش <son téléphone> 10 طلا`.
6. Il peut désormais négocier au prix coté.

### Commandes de l'opérateur

Les prix sont exprimés par mesghal ; les quantités d'or sont en grammes. Les nombres peuvent être saisis en chiffres persans ou latins.

| Commande | Effet |
| --- | --- |
| `<buy>-<sell>` | Publier une cotation, par ex. `79000000-79500000` |
| `ت <phone>` | Approuver un compte |
| `ر <phone>` | Rejeter un compte |
| `ن <phone> <role>` | Changer un rôle — `کاربر عادی`, `حسابدار`, `مدیر`, `مدیر ارشد` |
| `ش <phone> <amount> <asset>` | Créditer un compte, par ex. `ش 09121234567 500000 تومان` |
| `د <phone> <amount> <asset>` | Débiter un compte |
| `م <phone>` | Afficher les soldes |
| `س <phone>` | Afficher les ordres ouverts d'un utilisateur |
| `ک [search]` | Lister les utilisateurs |

## Exécution en local

Compilez une fois, puis démarrez chaque service dans son propre terminal. Une compilation pendant que les services tournent échoue à cause des DLL verrouillées.

```
dotnet build TallaEgg.sln

dotnet run --no-build --project src/User/Users.Api/Users.Api.csproj
dotnet run --no-build --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --no-build --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

`Affiliate.Api` n'est pas nécessaire (voir ci-dessus). `src/TallaEgg/TallaEgg.Api` est une ancienne API d'orchestration que rien n'appelle.

L'interface Swagger se trouve à `/api-docs` sur Users, Wallet et Orders — par exemple `http://localhost:5136/api-docs`. Elle n'est exposée **que lorsque `ASPNETCORE_ENVIRONMENT=Development`** ; elle n'existe donc pas sur un serveur déployé. `dotnet run` définit cette valeur à partir de `launchSettings.json`, elle est donc présente par défaut en local. Le bot n'expose aucun point de terminaison HTTP.

<a id="production-deployment"></a>
## Déploiement en production

Sur un serveur, les quatre services déployés (et non Affiliate.Api ni TallaEgg.Api — voir ci-dessus) s'exécutent comme services Windows natifs plutôt que dans un terminal par processus, afin de redémarrer automatiquement après un plantage ou un redémarrage. Voir [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) — issue #70 — pour la configuration initiale et les scripts d'installation de `scripts/windows-services/`.

## Aperçu des services

- **Users.Api** — inscription avec codes d'invitation, mise à jour du téléphone, gestion des rôles et des statuts, création des portefeuilles par défaut, recherche par identifiant Telegram, téléphone ou rôle.
- **Wallet.Api** — soldes, dépôts, retraits, blocage/déblocage, règlement atomique des transactions, historique des transactions.
- **Orders.Api** — publication et historique des cotations, exécutions de cotations, historique des transactions, meilleurs prix acheteur/vendeur, et le moteur d'appariement (inactif pour les symboles Dealer).
- **Bot Telegram** — hôte en long polling, clients API typés, notifications de transactions et les commandes de l'opérateur décrites ci-dessus.
- **Affiliate.Api** — codes d'invitation. Présent mais non fonctionnel ; voir la note dans Mise en place de la base de données.

<a id="testing"></a>
## Tests

```
dotnet test TallaEgg.sln
```

**`tests/TallaEgg.AllServices.Tests` est le seul projet de tests de la solution** et contient la suite de toute la plateforme — portefeuille, ordres, appariement, exécutions de cotations, gestionnaires du bot et mise en forme. Les nouveaux tests doivent y être placés, quel que soit le service qu'ils couvrent.

Le nom est volontairement explicite. Ce projet s'appelait `src/Wallet/Wallet.Tests` jusqu'à #117, alors que seuls huit de ses cinquante-six fichiers concernaient encore le portefeuille — un nom si trompeur qu'un audit a signalé le bot comme non testé parce que ses tests ne se trouvaient pas dans un dossier nommé d'après le bot.

La CI exécute `dotnet test TallaEgg.sln` au lieu de désigner ce projet par son chemin ; un futur projet de tests est donc pris en compte simplement en l'ajoutant à la solution, sans rien d'autre à retenir.

## Journalisation

Chaque service écrit dans la console et dans un fichier tournant d'un répertoire `logs/` **situé à côté de son propre exécutable**, et non dans le répertoire depuis lequel il a été lancé.

Sous `dotnet run`, il s'agit de la sortie de compilation — `src/Order/Orders.Api/bin/Debug/net9.0/logs/orders-api-<date>.log`. Sur une machine déployée, il s'agit du dossier de publication — `C:\TallaEgg\publish\Orders.Api\logs\orders-api-<date>.log`. Voir [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) pour le cas du déploiement, qui écrivait dans `C:\Windows\System32\logs\` jusqu'à [#211](https://github.com/MohKardan/TallaEgg/issues/211).

## Déploiement

Grâce au long polling, le bot n'a besoin d'**aucun port entrant**. Le travail de déploiement de KR1 ([#68](https://github.com/MohKardan/TallaEgg/issues/68) base de données, [#69](https://github.com/MohKardan/TallaEgg/issues/69) URL de production, [#70](https://github.com/MohKardan/TallaEgg/issues/70) supervision des processus et [#71](https://github.com/MohKardan/TallaEgg/issues/71) CI) est terminé — voir [Déploiement en production](#production-deployment) ci-dessus et [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) pour les étapes concrètes. Le mode `Production` a été exécuté et vérifié en local avec SQL Server Express, et pas seulement Development — la remarque qui figurait ici, selon laquelle il n'aurait jamais été exécuté, n'est plus vraie.

Utilisez `scripts/windows-services/` pour publier et installer les services — c'est désormais le seul outil de publication du dépôt. Un ancien `publish-all.ps1` à la racine et un dossier `publishes/` étaient antérieurs à #69/#70 et ont été supprimés : ils publiaient cinq services au lieu de quatre (dont les deux que #69 a décidé de ne pas déployer), faisaient encore référence aux adresses d'écoute HTTPS supprimées par #69, et supposaient un démarrage manuel par `dotnet *.dll` au lieu d'un service supervisé.
