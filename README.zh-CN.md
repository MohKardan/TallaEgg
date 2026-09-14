# TallaEgg 交易平台

[English](README.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | [Türkçe](README.tr.md) | [Français](README.fr.md) | **中文** | [Deutsch](README.de.md) | [हिन्दी](README.hi.md)

> 本文是 [`README.md`](README.md) 的译文。如两者存在出入，以英文版为准。

## 概述

TallaEgg 让一家黄金店铺通过 Telegram 与自己的客户进行交易。

店铺发布双向报价——即它的买入价和卖出价，以一个 `71000000-80000000` 这样的价格对输入。经过审核的客户可以看到该报价，并选择其中任意一方成交。每笔交易的对手方都是店铺；客户之间从不直接交易。

新用户启动机器人后需要注册、分享手机号码，然后等待店铺审核。在此之前没有任何访问权限。要进行交易，用户需要有余额或信用额度，信用额度由店铺手动授予——信用额度是黄金余额允许为负的上限，因此十克信用额度意味着客户可以卖出十克自己尚未持有的黄金。

经过审核的客户可以按发布的报价交易、查看交易历史，并与店铺进行实物结算。

https://private-user-images.githubusercontent.com/45781438/530709572-98e34f7f-e778-45ec-8516-4a3372e6764b.mp4

https://private-user-images.githubusercontent.com/45781438/530709883-c2a1096b-5c32-4a05-9fe5-81720a5f2567.mp4

## 主要功能

- **做市商报价模型（Dealer）**——店铺发布报价；订单仅在成交时创建并立即消耗，因此不会有抵押品锁定在挂单簿中。
- 通过事务性发件箱（transactional outbox）实现原子化交易结算，以交易 ID 保证幂等，并在订单可撮合之前锁定抵押品。
- 钱包领域：充值、提现、余额锁定、以黄金计价的信用额度、交易流水以及默认钱包的自动创建。
- 面向用户、钱包和订单的 RESTful 最小 API（minimal APIs），统一返回 `ApiResponse<T>` 响应封装。
- Telegram 机器人调用平台 API，通过长轮询（long polling）驱动完整的客户与运营者体验。
- 集中式配置（`config/appsettings.global.json`）、Serilog 日志，以及各服务间的类型化 HTTP 客户端。
- 代码库中仍保留一个用于点对点订单簿的撮合引擎，但它**不会对 Dealer 交易品种运行**——参见[交易模型](#trading-model)。

## 仓库结构

| 路径 | 说明 |
| --- | --- |
| `src/User` | Users 服务——注册、手机号/角色/状态、默认钱包 |
| `src/Wallet` | Wallet 服务——余额、锁定、结算、交易流水 |
| `src/Order` | Orders 服务——报价、报价成交、交易、撮合引擎 |
| `src/Affiliate` | Affiliate 服务——邀请码（**目前无法使用**，见下文） |
| `src/TallaEgg` | 共享的 core/application/infrastructure 库，以及一个遗留的编排 API |
| `tests/TallaEgg.AllServices.Tests` | 整个平台的测试套件（参见[测试](#testing)） |
| `TelegramBot` | Telegram 机器人宿主、处理程序和类型化 API 客户端 |
| `config/appsettings.global.json` | 所有服务共用的配置——已被 git 忽略（[#33](https://github.com/MohKardan/TallaEgg/issues/33)）；请从 `config/appsettings.global.example.json` 复制 |
| `docs/` | 架构、运维、流程、OKR 以及商业计划书 |
| `governance/` | 章程、细则、会议记录以及 `P-XXXX` 提案 |
| `scripts/` | 辅助脚本以及 Windows 服务的发布/安装工具 |

<a id="trading-model"></a>
## 交易模型

平台为每个交易品种支持两种市场模式，在配置中设置：

| 模式 | 行为 |
| --- | --- |
| **`Dealer`**（当前） | 店铺发布报价。客户接受报价时，会在一次操作中同时创建买卖双方订单并完成撮合。后台撮合引擎**完全跳过这些品种**——否则它会处理同一对正在成交的订单，把一笔交易变成两笔。 |
| `OrderBook` | 通过后台引擎进行经典的 maker/taker 撮合。目前未在生产环境中使用。 |

`MAUA/IRT`（黄金 / 托曼）、`SEKE_BAHAR/IRT`（Bahar Azadi 金币 / 托曼）和 `BTC/IRT`（比特币 / 托曼）均以 `Dealer` 模式运行。成交的对手方就是发布报价的一方，因此无需在配置中指定店铺。

### 添加交易品种

客户能否交易某个品种取决于两件相互独立、且被刻意分开的事情：

| 内容 | 存放位置 | 修改方式 |
| --- | --- | --- |
| **元数据**——小数精度、最小/最大数量、波斯语显示名称、由哪个 nerkh.io/brsapi.ir 行情标的定价 | `appsettings.global.json` 中的 `Symbols:{Base}/{Quote}`（参见其中已为上述三个品种提供的配置块） | 编辑文件，重启受影响的服务。无需改代码，无需重新构建。 |
| **是否启用**——是否显示在客户的品种选择器中、是否参与自动报价、能否用于手动报价 | 每个品种在数据库中对应一行（`SymbolSettings`，与 `AutoQuoteSettings` 相邻） | 通过机器人命令即时生效，无需重启：`نماد فعال [سکه\|بیت]` / `نماد غیرفعال [...]`。不带关键字时指 MAUA/IRT。 |

符合标准形态的品种——以托曼计价、并像黄金/金币/比特币一样通过 nerkh.io 和/或 brsapi.ir 定价——**只需要一个配置块**，再由管理员将其启用即可。`TallaEgg.Core.CurrenciesConstant` 已将上述三个品种作为编译内置的默认值（因此测试套件和全新克隆的仓库完全不需要配置文件）；为*新*键添加的配置块会在其上新增第四项，而为*已有*键添加的配置块只覆盖它所设置的字段。

`Matching:MarketModes`（见上文）又是另一项独立设置——它决定 `Dealer` 还是 `OrderBook`，并且每个品种仍需要单独的条目。

如果某个品种的价格来源既不属于 nerkh.io 也不属于 brsapi.ir，仍需新建一个实现 `Orders.Core.IReferencePriceProvider` 的类——这是唯一不可避免需要写代码的部分，因为它是一项新的外部集成，而不是新的品种定义。

## 技术栈

- .NET 9.0，C# 12，最小 API 和后台服务。
- Entity Framework Core 9，使用 SQL Server 提供程序。
- Serilog，向控制台和滚动文件输出结构化日志。
- 基于长轮询的 Telegram.Bot——**无需任何入站端口**。
- 用于服务间调用的类型化 `HttpClient` 封装。

## 前置条件

- .NET SDK 9.0。
- 主机可访问的 **SQL Server Express**（名为 `SQLEXPRESS` 的命名实例，Windows 身份验证）。不要使用 LocalDB——LocalDB 是按用户、按需启动的实例，在服务器上并不存在，这正是 [#68](https://github.com/MohKardan/TallaEgg/issues/68) 之前部署失败的原因。Express 与真实（单机）部署运行的是同一个数据库引擎，只是安装在本地。
- 从 [@BotFather](https://t.me/BotFather) 获取的 Telegram 机器人令牌。
- 你自己的 Telegram 数字用户 ID（可向 [@userinfobot](https://t.me/userinfobot) 查询）——在全新数据库上，正是它让你成为店铺的运营者。

## 配置

每个服务都会加载 `config/appsettings.global.json`，然后将 `Services:` 下与自身程序集名称匹配的配置节展开使用。无需维护各服务单独的 `appsettings.json`。

> ⚠️ **旧的机器人令牌和旧的共享 API 密钥仍可在本仓库中读到**——一处在 git 历史中，因为该文件过去曾被跟踪；另一处在一个被跟踪的源文件中：`TallaEgg.TelegramBot.Infrastructure/Program.cs` 里 `TelegramLoggerService` 的注册代码，它没有自己的配置键。**这些凭据已全部在 [#33](https://github.com/MohKardan/TallaEgg/issues/33) 中轮换并已失效——请勿将其报告为正在泄露的凭据。** 新读者每次审查都会重新发现它们并提起安全事件，而每次都是误报。历史**刻意没有**被改写——该决定及其理由记录在 [#105](https://github.com/MohKardan/TallaEgg/issues/105)。配置文件本身现已被 git 忽略；请勿将它或任何新的密钥重新加入版本控制。

请根据下面的模板创建你自己的副本。

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

### 关键设置

| 设置 | 为什么重要 |
| --- | --- |
| `BotSettings:OwnerTelegramIds` | 在空数据库上，这是让任何人进入系统的唯一途径。配置在此的所有者注册时会被自动审核通过并授予 `Admin` 角色。请在此填写**你自己的** Telegram ID。 |
| `BotSettings:DefaultReferralCode` | 必须为 `admin`——即 `Users.Api` 初始写入的管理员记录所持有的邀请码。注册时会拒绝不属于任何用户的邀请码，因此此处不匹配意味着**任何人都无法注册**。 |
| `Matching:MarketModes` | 如果没有将品种设置为 `"Dealer"`（例如 `"MAUA/IRT": "Dealer"`），它会回退为 `OrderBook`，该品种的所有报价成交都会被拒绝。 |
| `TelegramBotToken` | 通常从该文件读取，缺少它机器人将拒绝启动。不存在 `TELEGRAM_BOT_TOKEN` 回退——系统不会读取这个名称。不过该键仍可按进程覆盖，因为机器人的配置链最后依次是环境变量和命令行（#181），所以环境中的 `TelegramBotToken=...` 或命令行中的 `--TelegramBotToken=...` 都优先于文件。 |

### 端口与绑定地址

上面每个 `Urls` 条目都刻意使用环回地址（`localhost`）上的纯 HTTP——参见 [#69](https://github.com/MohKardan/TallaEgg/issues/69)：

- 机器人通过**长轮询**连接 Telegram；由它主动发起连接，Telegram 从不主动连入。
- 四个实际 API 只会被同一主机上的机器人调用。
- 因此**无需开放任何入站端口**，也无需 HTTPS 绑定地址——`https://localhost:...` 这样的条目需要开发证书，而服务器上不会有；并且在面向互联网的端口与数据库之间，唯一的防线将只剩上面提到的共享 API 密钥。绑定到环回地址才是这里真正的安全措施，而不是待替换的占位值。

| 服务 | 端口 | 用途 |
| --- | --- | --- |
| Users.Api | 5136 | 注册、角色、钱包创建 |
| Wallet.Api | 60933 | 余额、结算 |
| Orders.Api | 5140 | 报价、交易、撮合 |
| Affiliate.Api | 60812 | 不部署——参见[数据库设置](#database-setup) |
| TallaEgg.TelegramBot.Infrastructure | 57546 | 已配置，但没有任何程序监听——机器人是一个没有 Web 服务器的普通通用主机，通过长轮询连接 Telegram |
| TallaEgg.Api | 5135 | 不部署——遗留项目，没有任何调用方 |

在真实服务器上，请确认这些端口都不会出现在 `netstat`/`ss` 的公网接口上——只应出现在 `127.0.0.1` 上。

### 共享 API 密钥

Wallet.Api、Users.Api、Orders.Api 和 Affiliate.Api 使用通过 `X-API-Key` 请求头发送的共享密钥来认证服务间调用（`TallaEgg.Core.APIKeyConstant`）。该密钥从环境变量 `TALLAEGG_API_KEY` 读取，而不是从任何被跟踪的文件读取。

- **开发环境**：四个服务都不会强制校验——只有在 `ASPNETCORE_ENVIRONMENT=Production` 时才启用 API 密钥认证。本地请不要设置该变量。
- **生产环境**：在启动任何服务之前设置 `TALLAEGG_API_KEY`；如果缺失，每个服务都会在启动时——在绑定端口之前——抛出异常。

> **服务器上必须显式设置 `ASPNETCORE_ENVIRONMENT`。** 在本地，由于 `launchSettings.json`，`dotnet run` 总是报告 `Development`——但发布后的部署不会使用该文件，因此在 `dotnet run` 之外，服务器一旦设置（或隐式默认）即为 `Production`。在 [#69](https://github.com/MohKardan/TallaEgg/issues/69) 之前，`Production` 代码路径——API 密钥认证、不对 Swagger 重定向做豁免——从未真正执行过。

<a id="database-setup"></a>
## 数据库设置

每个 API 在启动时都会调用 `Database.MigrateAsync()`，因此首次运行时会自动创建数据库结构。Users、Wallet 和 Orders 无需任何手动步骤。

如需提前应用迁移：

```
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/User/Users.Api/Users.Api.csproj
dotnet ef database update --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet ef database update --project src/Order/Orders.Api/Orders.Api.csproj
```

`Users.Api` 会写入一条管理员记录（`5564f136-b9fb-4719-b4dc-b0833fa24761`），其唯一用途是持有初始邀请码。它没有 Telegram ID，也无法以它的身份登录。

> **Affiliate 没有迁移。** `Affiliate.Api` 会调用 `MigrateAsync()`，但不包含任何迁移文件，因此它能正常启动，随后每个请求都会以 `Invalid object name 'Invitations'` 失败。目前没有任何地方调用它——机器人唯一的邀请调用已被注释掉——因此部署时可以不包含它。

## 首次运行

在空数据库上，以下就是全部设置步骤。无需运行任何 SQL，也无需在文件之间复制 ID。

1. 将你的 Telegram ID 填入 `BotSettings:OwnerTelegramIds` 并启动各服务。
2. 向机器人发送 `/start`，在提示时分享你的手机号码。
   → 你将被自动审核通过并获得 `Admin` 角色。
3. 将买入价和卖出价作为一个价格对发送以发布报价，例如 `79000000-79500000`。
4. 让一位客户发送 `/start` 并分享其号码，然后审核通过：`ت <对方手机号>`。
5. 为其授予信用额度：`ش <对方手机号> 10 طلا`。
6. 该客户现在即可按发布的报价进行交易。

### 运营者命令

价格以每米斯卡尔（mesghal）计；黄金数量以克计。数字可使用波斯数字或拉丁数字输入。

| 命令 | 作用 |
| --- | --- |
| `<buy>-<sell>` | 发布报价，例如 `79000000-79500000` |
| `ت <phone>` | 审核通过账户 |
| `ر <phone>` | 拒绝账户 |
| `ن <phone> <role>` | 更改角色——`کاربر عادی`、`حسابدار`、`مدیر`、`مدیر ارشد` |
| `ش <phone> <amount> <asset>` | 为账户入账，例如 `ش 09121234567 500000 تومان` |
| `د <phone> <amount> <asset>` | 从账户扣款 |
| `م <phone>` | 显示余额 |
| `س <phone>` | 显示用户的未完成订单 |
| `ک [search]` | 列出用户 |

## 本地运行

先构建一次，然后在各自的终端中分别启动每个服务。服务运行期间进行构建会因 DLL 被锁定而失败。

```
dotnet build TallaEgg.sln

dotnet run --no-build --project src/User/Users.Api/Users.Api.csproj
dotnet run --no-build --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --no-build --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

不需要 `Affiliate.Api`（见上文）。`src/TallaEgg/TallaEgg.Api` 是一个没有任何调用方的遗留编排 API。

Users、Wallet 和 Orders 的 Swagger UI 位于 `/api-docs`——例如 `http://localhost:5136/api-docs`。它**仅在 `ASPNETCORE_ENVIRONMENT=Development` 时**映射，因此在已部署的服务器上并不存在；`dotnet run` 会从 `launchSettings.json` 设置该值，所以本地默认可用。机器人完全不暴露任何 HTTP 端点。

<a id="production-deployment"></a>
## 生产部署

在服务器上，四个需要部署的服务（不包括 Affiliate.Api 和 TallaEgg.Api——见上文）以原生 Windows 服务的形式运行，而不是每个进程开一个终端，因此在崩溃或重启后会自动恢复运行。一次性设置步骤以及 `scripts/windows-services/` 中的安装脚本，请参阅 [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md)——issue #70。

## 服务要点

- **Users.Api**——基于邀请码的注册、手机号更新、角色与状态管理、默认钱包创建，以及按 Telegram ID、手机号或角色查询。
- **Wallet.Api**——余额、充值、提现、锁定/解锁、原子化交易结算、交易流水。
- **Orders.Api**——报价发布与历史、报价成交、交易历史、最优买卖价，以及（对 Dealer 品种处于休眠状态的）撮合引擎。
- **Telegram 机器人**——长轮询宿主、类型化 API 客户端、交易通知，以及上文所述的运营者命令。
- **Affiliate.Api**——邀请码。存在但无法使用；参见数据库设置中的说明。

<a id="testing"></a>
## 测试

```
dotnet test TallaEgg.sln
```

**`tests/TallaEgg.AllServices.Tests` 是解决方案中唯一的测试项目**，包含整个平台的测试套件——钱包、订单、撮合、报价成交、机器人处理程序和格式化。无论新测试覆盖哪个服务，都应放在这里。

这个名称是刻意取得直白的。该项目在 #117 之前名为 `src/Wallet/Wallet.Tests`，而当时它的五十六个文件中只有八个与钱包有关——名称误导到一次审计因为机器人的测试不在以机器人命名的文件夹下，而报告机器人没有测试。

CI 运行 `dotnet test TallaEgg.sln`，而不是按路径指定该项目，因此将来新增测试项目只需把它加入解决方案即可被自动纳入，无需记住其他任何事项。

## 日志

每个服务都会将日志写入控制台，以及**其自身可执行文件旁边**的 `logs/` 目录中的滚动文件，而不是启动时所在的目录。

使用 `dotnet run` 时，这是构建输出目录——`src/Order/Orders.Api/bin/Debug/net9.0/logs/orders-api-<date>.log`。在已部署的机器上，这是发布目录——`C:\TallaEgg\publish\Orders.Api\logs\orders-api-<date>.log`。部署场景请参阅 [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md)，在 [#211](https://github.com/MohKardan/TallaEgg/issues/211) 之前，该场景会把日志写到 `C:\Windows\System32\logs\`。

## 部署

长轮询意味着机器人**不需要任何入站端口**。KR1 的部署工作（[#68](https://github.com/MohKardan/TallaEgg/issues/68) 数据库、[#69](https://github.com/MohKardan/TallaEgg/issues/69) 生产环境 URL、[#70](https://github.com/MohKardan/TallaEgg/issues/70) 进程守护，以及 [#71](https://github.com/MohKardan/TallaEgg/issues/71) CI）均已完成——具体步骤请参见上文的[生产部署](#production-deployment)和 [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md)。`Production` 模式已在本地针对 SQL Server Express 运行并验证过，而不仅是 Development——此处原先关于它从未执行过的说明已不再成立。

请使用 `scripts/windows-services/` 发布和安装服务——它现在是仓库中唯一的发布工具。仓库根目录下旧的 `publish-all.ps1` 和 `publishes/` 文件夹早于 #69/#70，已被删除：它们发布的是五个服务而不是四个（包括 #69 决定不部署的两个），仍引用 #69 已移除的 HTTPS 绑定地址，并且假定通过 `dotnet *.dll` 手动启动，而不是作为受守护的服务运行。
