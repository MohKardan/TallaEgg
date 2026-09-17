# TallaEgg Alım Satım Platformu

[English](README.md) | [فارسی](README.fa.md) | [العربية](README.ar.md) | **Türkçe** | [Français](README.fr.md) | [中文](README.zh-CN.md) | [Deutsch](README.de.md) | [हिन्दी](README.hi.md) | [Русский](README.ru.md)

> Bu belge [`README.md`](README.md) dosyasının çevirisidir. İkisi arasında bir farklılık olursa İngilizce sürüm esas alınır.

## Genel Bakış

TallaEgg, bir kuyumcunun kendi müşterileriyle Telegram üzerinden alım satım yapmasını sağlar.

Kuyumcu iki yönlü bir fiyat yayınlar — alış fiyatı ve satış fiyatı, tek bir `71000000-80000000` çifti olarak girilir. Onaylanmış müşteriler bu fiyatı görür ve iki taraftan birini alır. Her işlemde karşı taraf kuyumcudur; müşteriler asla birbirleriyle işlem yapmaz.

Botu başlatan yeni bir kullanıcı kaydolur, telefon numarasını paylaşır ve kuyumcunun onayını bekler. Onaylanana kadar hiçbir erişimi yoktur. İşlem yapabilmek için ya bakiyeye ya da kuyumcunun elle tanımladığı krediye ihtiyacı vardır — kredi, altın bakiyesinin eksiye düşebileceği bir tavandır; yani on gram kredi, müşterinin henüz elinde olmayan on gramı satabilmesini sağlar.

Onaylanmış müşteriler yayınlanan fiyattan işlem yapabilir, işlem geçmişlerini inceleyebilir ve kuyumcuyla fiziksel olarak hesaplaşabilir.

https://private-user-images.githubusercontent.com/45781438/530709572-98e34f7f-e778-45ec-8516-4a3372e6764b.mp4

https://private-user-images.githubusercontent.com/45781438/530709883-c2a1096b-5c32-4a05-9fe5-81720a5f2567.mp4

## Temel Yetenekler

- **Dealer (piyasa yapıcı) fiyat modeli** — kuyumcu bir fiyat yayınlar; emirler yalnızca gerçekleşme anında oluşturulur ve hemen tüketilir, böylece bekleyen bir emir defterinde kilitli teminat kalmaz.
- İşlem kimliğine göre idempotent, işlemsel bir outbox üzerinden atomik işlem takası; teminat, bir emir eşleşebilir hale gelmeden önce kilitlenir.
- Para yatırma, çekme, bakiye kilitleme, altın cinsinden kredi, işlem geçmişi ve varsayılan cüzdan oluşturmayı içeren cüzdan alanı.
- Kullanıcılar, cüzdanlar ve emirler için birleşik bir `ApiResponse<T>` zarfı döndüren RESTful minimal API'ler.
- Platform API'lerini kullanan ve tüm müşteri ve operatör deneyimini long polling ile yürüten Telegram botu.
- Tüm servislerde merkezi yapılandırma (`config/appsettings.global.json`), Serilog günlükleme ve tipli HTTP istemcileri.
- Eşten eşe emir defterleri için bir eşleştirme motoru kod tabanında durmaktadır ancak **Dealer sembolleri için çalışmaz** — bkz. [Alım satım modeli](#trading-model).

## Depo Yapısı

| Yol | Açıklama |
| --- | --- |
| `src/User` | Users servisi — kayıt, telefon/rol/durum, varsayılan cüzdanlar |
| `src/Wallet` | Wallet servisi — bakiyeler, kilitleme, takas, işlem geçmişi |
| `src/Order` | Orders servisi — fiyatlar, fiyat gerçekleşmeleri, işlemler, eşleştirme motoru |
| `src/Affiliate` | Affiliate servisi — davet kodları (**şu anda çalışmıyor**, aşağıya bakın) |
| `src/TallaEgg` | Ortak core/application/infrastructure kütüphaneleri ve eski bir orkestrasyon API'si |
| `tests/TallaEgg.AllServices.Tests` | Tüm platformun test paketi (bkz. [Testler](#testing)) |
| `TelegramBot` | Telegram bot sunucusu, işleyiciler ve tipli API istemcileri |
| `config/appsettings.global.json` | Tüm servislerin kullandığı ortak yapılandırma — git tarafından yok sayılır ([#33](https://github.com/MohKardan/TallaEgg/issues/33)); `config/appsettings.global.example.json` dosyasından kopyalayın |
| `docs/` | Mimari, operasyon, süreç, OKR'ler ve iş teklifi |
| `governance/` | Tüzük, yönetmelik, toplantı notları ve `P-XXXX` önerileri |
| `scripts/` | Yardımcı betikler ve Windows servislerini yayınlama/kurma araçları |

<a id="trading-model"></a>
## Alım satım modeli

Platform, sembol başına yapılandırmada belirlenen iki piyasa modunu destekler:

| Mod | Davranış |
| --- | --- |
| **`Dealer`** (mevcut) | Kuyumcu bir fiyat yayınlar. Bir müşteri bunu kabul ettiğinde her iki emir de oluşturulur ve tek bir işlemde eşleştirilir. Arka plandaki eşleştirme motoru **bu sembolleri tamamen atlar** — aksi halde bir gerçekleşmenin zaten eşleştirmekte olduğu emir çiftine ulaşır ve tek işlemden iki işlem üretirdi. |
| `OrderBook` | Arka plan motoru üzerinden klasik maker/taker eşleştirmesi. Bugün üretimde kullanılmıyor. |

`MAUA/IRT` (altın / toman), `SEKE_BAHAR/IRT` (Bahar Azadi sikkesi / toman) ve `BTC/IRT` (Bitcoin / toman) `Dealer` modunda çalışır. Bir gerçekleşmenin karşı tarafı fiyatı yayınlayan kişidir; bu yüzden yapılandırmada kuyumcunun adını belirtmek gerekmez.

### Alım satım sembolü ekleme

Müşterilerin bir sembolde işlem yapıp yapamayacağını, bilinçli olarak birbirinden ayrı tutulan iki bağımsız şey belirler:

| Ne | Nerede bulunur | Nasıl değiştirilir |
| --- | --- | --- |
| **Meta veri** — ondalık hassasiyet, en az/en çok miktar, Farsça görünen ad, fiyatı hangi nerkh.io/brsapi.ir enstrümanının belirlediği | `appsettings.global.json` içinde `Symbols:{Base}/{Quote}` (yukarıdaki üç sembol için orada zaten bulunan bloğa bakın) | Dosyayı düzenleyin, ilgili servis(ler)i yeniden başlatın. Kod değişikliği yok, yeniden derleme yok. |
| **Etkin mi değil mi** — müşterinin sembol seçicisinde gösterilir, otomatik fiyatlamaya uygundur, elle fiyat için kullanılabilir | Sembol başına bir veritabanı satırı (`SymbolSettings`, `AutoQuoteSettings` yanında) | Bir bot komutu, anında, yeniden başlatma olmadan: `نماد فعال [سکه\|بیت]` / `نماد غیرفعال [...]`. Anahtar kelime yoksa MAUA/IRT kastedilir. |

Standart yapıya uyan bir sembol — toman cinsinden olan ve altın/sikke/Bitcoin'de olduğu gibi nerkh.io ve/veya brsapi.ir ile fiyatlanan — **yalnızca bir yapılandırma bloğuna** ve ardından onu etkinleştiren bir yöneticiye ihtiyaç duyar. `TallaEgg.Core.CurrenciesConstant`, yukarıdaki üç sembolü derlenmiş varsayılanlar olarak içerir (böylece test paketi ve yeni bir klon hiçbir yapılandırma dosyasına ihtiyaç duymaz); *yeni* bir anahtar için bir yapılandırma bloğu bunların üzerine dördüncü bir giriş ekler, *mevcut* bir anahtar için bir blok ise yalnızca belirlediği alanları geçersiz kılar.

`Matching:MarketModes` (yukarıda) yine bundan ayrıdır — `Dealer` ile `OrderBook` arasında karar verir ve hâlâ her sembol için kendi girişine ihtiyaç duyar.

Fiyatı ne nerkh.io'nun ne de brsapi.ir'nin kapsadığı bir kaynaktan gelen bir sembol, hâlâ `Orders.Core.IReferencePriceProvider` arayüzünü uygulayan yeni bir sınıf gerektirir — bu işin kaçınılmaz olarak kod gerektiren tek kısmıdır, çünkü yeni bir sembol tanımı değil, yeni bir dış entegrasyondur.

## Teknoloji Yığını

- C# 12, minimal API'ler ve arka plan servisleriyle .NET 9.0.
- SQL Server sağlayıcısıyla Entity Framework Core 9.
- Konsola ve dönen dosyalara yapılandırılmış günlükleme için Serilog.
- Long polling üzerinden Telegram.Bot — **gelen port gerekmez**.
- Servisler arası çağrılar için tipli `HttpClient` sarmalayıcıları.

## Ön Koşullar

- .NET SDK 9.0.
- Sunucudan erişilebilen **SQL Server Express** (adlandırılmış bir `SQLEXPRESS` örneği, Windows Kimlik Doğrulaması). LocalDB değil — LocalDB kullanıcıya özel, isteğe bağlı başlatılan bir örnektir ve sunucuda bulunmaz; [#68](https://github.com/MohKardan/TallaEgg/issues/68) öncesinde dağıtımı bozan tam olarak buydu. Express, gerçek (tek makineli) bir dağıtımın çalıştırdığı motorun aynısıdır, yalnızca yerel olarak kurulmuştur.
- [@BotFather](https://t.me/BotFather)'dan alınmış bir Telegram bot jetonu.
- Kendi sayısal Telegram kullanıcı kimliğiniz ([@userinfobot](https://t.me/userinfobot)'a sorun) — boş bir veritabanında sizi kuyumcu operatörü yapan budur.

## Yapılandırma

Her servis `config/appsettings.global.json` dosyasını yükler, ardından `Services:` altındaki kendi assembly adıyla eşleşen bölümü düzleştirir. Bakımı yapılacak servis başına bir `appsettings.json` yoktur.

> ⚠️ **Eski bot jetonları ve eski ortak API anahtarı bu deponun git geçmişinde hâlâ okunabilir durumdadır**, çünkü bu dosya eskiden izleniyordu. İzlenen kaynak dosyalarda artık hiçbir jeton yoktur. **Geçmişte bulunan her şeyi ele geçirilmiş sayın ve bir belgenin yenilendiğini söylemesine güvenmeyin**: kontrol edin ve hâlâ çalışıyorsa BotFather üzerinden iptal edin (veya anahtarı yenileyin). Geçmiş bilinçli olarak yeniden **yazılmamıştır** — çözüm yenilemedir; bu karar ve gerekçesi [#105](https://github.com/MohKardan/TallaEgg/issues/105) içindedir. Yapılandırma dosyası artık git tarafından yok sayılmaktadır; onu veya yeni herhangi bir gizli bilgiyi sürüm kontrolüne yeniden eklemeyin.

Aşağıdaki şablondan kendi kopyanızı oluşturun.

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

### Önemli ayarlar

| Ayar | Neden önemli |
| --- | --- |
| `BotSettings:OwnerTelegramIds` | Boş bir veritabanında herhangi birinin içeri girmesini sağlayan tek şey. Yapılandırılmış bir sahip, kaydolduğunda otomatik olarak onaylanır ve `Admin` rolünü alır. Buraya **kendi** Telegram kimliğinizi yazın. |
| `BotSettings:DefaultReferralCode` | `admin` olmalıdır — `Users.Api`'nin oluşturduğu yönetici satırının taşıdığı kod. Kayıt, hiçbir kullanıcıya ait olmayan her kodu reddeder; bu yüzden buradaki bir uyuşmazlık **hiç kimsenin kaydolamaması** anlamına gelir. |
| `Matching:MarketModes` | `"Dealer"` olarak ayarlanmış bir sembol olmadan (ör. `"MAUA/IRT": "Dealer"`) `OrderBook`'a geri düşer ve o sembol için her fiyat gerçekleşmesi reddedilir. |
| `TelegramBotToken` | Normalde bu dosyadan okunur ve bot onsuz başlamayı reddeder. `TELEGRAM_BOT_TOKEN` yedeği yoktur — bu isme bakılmaz. Anahtar yine de süreç bazında geçersiz kılınabilir, çünkü botun yapılandırma zinciri ortam değişkenleri ve ardından komut satırıyla biter (#181); yani ortamdaki `TelegramBotToken=...` veya komut satırındaki `--TelegramBotToken=...` dosyaya göre önceliklidir. |

### Portlar ve dinleme adresleri

Yukarıdaki her `Urls` girişi bilinçli olarak loopback (`localhost`) üzerinde düz HTTP'dir — bkz. [#69](https://github.com/MohKardan/TallaEgg/issues/69):

- Bot Telegram'a **long polling** ile ulaşır; bağlantıyı kendisi başlatır, Telegram asla ona bağlanmaz.
- Dört gerçek API yalnızca bot tarafından, aynı sunucu üzerinde çağrılır.
- Bu yüzden **hiçbir gelen portun açık olması gerekmez**, HTTPS dinleme adresine de gerek yoktur — bir `https://localhost:...` girişi sunucuda bulunmayacak bir geliştirme sertifikası gerektirir ve internete açık bir port ile veritabanı arasında yalnızca yukarıdaki ortak API anahtarı kalırdı. Loopback'e bağlanmak buradaki asıl güvenlik önlemidir, değiştirilecek bir yer tutucu değildir.

| Servis | Port | Amaç |
| --- | --- | --- |
| Users.Api | 5136 | Kayıt, roller, cüzdan oluşturma |
| Wallet.Api | 60933 | Bakiyeler, takas |
| Orders.Api | 5140 | Fiyatlar, işlemler, eşleştirme |
| Affiliate.Api | 60812 | Dağıtılmıyor — bkz. [Veritabanı Kurulumu](#database-setup) |
| TallaEgg.TelegramBot.Infrastructure | 57546 | Yapılandırılmış ama hiçbir şey dinlemiyor — bot, web sunucusu olmayan sade bir generic host'tur ve Telegram'a long polling ile ulaşır |
| TallaEgg.Api | 5135 | Dağıtılmıyor — eski, hiçbir şey çağırmıyor |

Gerçek bir sunucuda bunların hiçbirinin `netstat`/`ss` çıktısında genel bir arayüzde görünmediğini doğrulayın — yalnızca `127.0.0.1` üzerinde.

### Ortak API anahtarı

Wallet.Api, Users.Api, Orders.Api ve Affiliate.Api, servisler arası çağrıları `X-API-Key` başlığıyla gönderilen ortak bir anahtarla doğrular (`TallaEgg.Core.APIKeyConstant`). Anahtar izlenen bir dosyadan değil, `TALLAEGG_API_KEY` ortam değişkeninden okunur.

- **Geliştirme**: dört servisin hiçbiri bunu zorunlu tutmaz — API anahtarı doğrulaması yalnızca `ASPNETCORE_ENVIRONMENT=Production` olduğunda devreye girer. Değişkeni yerelde tanımsız bırakın.
- **Üretim**: herhangi bir servisi başlatmadan önce `TALLAEGG_API_KEY` değişkenini ayarlayın; eksikse her servis başlangıçta — portunu dinlemeye başlamadan önce — hata fırlatır.

> **`ASPNETCORE_ENVIRONMENT` bir sunucuda açıkça ayarlanmalıdır.** Yerelde `dotnet run`, `launchSettings.json` nedeniyle her zaman `Development` bildirir — bu dosya yayınlanmış bir dağıtımda kullanılmaz; bu yüzden `dotnet run` dışında bir sunucu, değişken ayarlandığı (veya örtük bırakıldığı) anda varsayılan olarak `Production` olur. [#69](https://github.com/MohKardan/TallaEgg/issues/69)'a kadar `Production` kod yolu — API anahtarı doğrulaması, Swagger yönlendirme istisnası olmaması — hiç gerçekten çalıştırılmamıştı.

<a id="database-setup"></a>
## Veritabanı Kurulumu

Her API başlangıçta `Database.MigrateAsync()` çağırır; böylece şema ilk çalıştırmada oluşturulur. Users, Wallet veya Orders için elle bir adım gerekmez.

Migration'ları önceden uygulamak için:

```
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef database update --project src/User/Users.Api/Users.Api.csproj
dotnet ef database update --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet ef database update --project src/Order/Orders.Api/Orders.Api.csproj
```

`Users.Api`, tek amacı başlangıç davet koduna sahip olmak olan bir yönetici satırı (`5564f136-b9fb-4719-b4dc-b0833fa24761`) oluşturur. Bu satırın Telegram kimliği yoktur ve onun adına oturum açılamaz.

> **Affiliate'in migration'ı yoktur.** `Affiliate.Api` `MigrateAsync()` çağırır ancak hiçbir migration dosyası içermez; bu yüzden sorunsuz başlar ve ardından her istekte `Invalid object name 'Invitations'` hatasıyla başarısız olur. Şu anda onu hiçbir şey çağırmıyor — botun tek davet çağrısı yorum satırına alınmış — bu yüzden dağıtımın dışında bırakılabilir.

## İlk çalıştırma

Boş bir veritabanında kurulumun tamamı budur. Çalıştırılacak SQL ya da dosyalar arasında kopyalanacak bir kimlik yoktur.

1. Telegram kimliğinizi `BotSettings:OwnerTelegramIds` içine yazın ve servisleri başlatın.
2. Bota `/start` gönderin, ardından istendiğinde telefon numaranızı paylaşın.
   → Otomatik olarak onaylanır ve `Admin` rolünü alırsınız.
3. Alış ve satış fiyatını tek bir çift olarak göndererek bir fiyat yayınlayın, ör. `79000000-79500000`.
4. Bir müşterinin `/start` göndermesini ve numarasını paylaşmasını sağlayın, ardından onu onaylayın: `ت <telefonu>`.
5. Ona kredi tanımlayın: `ش <telefonu> 10 طلا`.
6. Artık yayınlanan fiyattan işlem yapabilir.

### Operatör komutları

Fiyatlar miskal (mesghal) başınadır; altın miktarları gram cinsindendir. Sayılar Farsça veya Latin rakamlarla yazılabilir.

| Komut | Etkisi |
| --- | --- |
| `<buy>-<sell>` | Bir fiyat yayınla, ör. `79000000-79500000` |
| `ت <phone>` | Bir hesabı onayla |
| `ر <phone>` | Bir hesabı reddet |
| `ن <phone> <role>` | Rolü değiştir — `کاربر عادی`, `حسابدار`, `مدیر`, `مدیر ارشد` |
| `ش <phone> <amount> <asset>` | Bir hesaba alacak kaydet, ör. `ش 09121234567 500000 تومان` |
| `د <phone> <amount> <asset>` | Bir hesaba borç kaydet |
| `م <phone>` | Bakiyeleri göster |
| `س <phone>` | Bir kullanıcının açık emirlerini göster |
| `ک [search]` | Kullanıcıları listele |

## Yerelde Çalıştırma

Bir kez derleyin, ardından her servisi kendi terminalinde başlatın. Servisler çalışırken derleme, kilitli DLL'ler nedeniyle başarısız olur.

```
dotnet build TallaEgg.sln

dotnet run --no-build --project src/User/Users.Api/Users.Api.csproj
dotnet run --no-build --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --no-build --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

`Affiliate.Api` gerekli değildir (yukarıya bakın). `src/TallaEgg/TallaEgg.Api` hiçbir şeyin çağırmadığı eski bir orkestrasyon API'sidir.

Swagger arayüzü Users, Wallet ve Orders üzerinde `/api-docs` adresindedir — örneğin `http://localhost:5136/api-docs`. **Yalnızca `ASPNETCORE_ENVIRONMENT=Development` olduğunda** eşlenir, bu yüzden dağıtılmış bir sunucuda bulunmaz; `dotnet run` bu değeri `launchSettings.json`'dan ayarladığı için yerelde varsayılan olarak mevcuttur. Bot hiçbir HTTP uç noktası sunmaz.

<a id="production-deployment"></a>
## Üretim Dağıtımı

Bir sunucuda, dağıtılan dört servis (Affiliate.Api veya TallaEgg.Api değil — yukarıya bakın) süreç başına bir terminal yerine yerel Windows servisleri olarak çalışır; böylece bir çökme veya yeniden başlatmadan sonra otomatik olarak yeniden başlarlar. Tek seferlik kurulum ve `scripts/windows-services/` kurulum betikleri için [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) belgesine bakın — issue #70.

## Servislere Genel Bakış

- **Users.Api** — davet kodlarıyla kayıt, telefon güncelleme, rol ve durum yönetimi, varsayılan cüzdan oluşturma, Telegram kimliği, telefon veya role göre arama.
- **Wallet.Api** — bakiyeler, para yatırma, çekme, kilitleme/kilit açma, atomik işlem takası, işlem geçmişi.
- **Orders.Api** — fiyat yayınlama ve geçmişi, fiyat gerçekleşmeleri, işlem geçmişi, en iyi alış/satış fiyatı ve (Dealer sembolleri için devre dışı) eşleştirme motoru.
- **Telegram Botu** — long polling sunucusu, tipli API istemcileri, işlem bildirimleri ve yukarıdaki operatör komutları.
- **Affiliate.Api** — davet kodları. Mevcut ama çalışmıyor; Veritabanı Kurulumu altındaki nota bakın.

<a id="testing"></a>
## Testler

```
dotnet test TallaEgg.sln
```

**`tests/TallaEgg.AllServices.Tests` çözümdeki tek test projesidir** ve tüm platformun test paketini içerir — cüzdan, emirler, eşleştirme, fiyat gerçekleşmeleri, bot işleyicileri ve biçimlendirme. Yeni testler, hangi servisi kapsadıklarından bağımsız olarak buraya aittir.

İsim bilinçli olarak açıktır. Bu proje #117'ye kadar `src/Wallet/Wallet.Tests` idi; o noktada elli altı dosyasından yalnızca sekizi cüzdanla ilgiliydi — o kadar yanıltıcıydı ki bir denetim, testleri bot adını taşıyan bir klasörde olmadığı için botu test edilmemiş olarak raporladı.

CI, bu projeyi yoluyla belirtmek yerine `dotnet test TallaEgg.sln` çalıştırır; böylece gelecekteki bir test projesi yalnızca çözüme eklenerek dahil edilir ve hatırlanması gereken başka bir şey kalmaz.

## Günlükleme

Her servis konsola ve başlatıldığı dizine değil, **kendi ikili dosyasının yanındaki** bir `logs/` dizininde dönen bir dosyaya yazar.

`dotnet run` altında bu, derleme çıktısıdır — `src/Order/Orders.Api/bin/Debug/net9.0/logs/orders-api-<date>.log`. Dağıtılmış bir makinede yayın klasörüdür — `C:\TallaEgg\publish\Orders.Api\logs\orders-api-<date>.log`. [#211](https://github.com/MohKardan/TallaEgg/issues/211)'e kadar `C:\Windows\System32\logs\` içine yazan dağıtım durumu için [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) belgesine bakın.

## Dağıtım

Long polling, botun **hiçbir gelen porta** ihtiyaç duymadığı anlamına gelir. KR1'in dağıtım çalışması ([#68](https://github.com/MohKardan/TallaEgg/issues/68) veritabanı, [#69](https://github.com/MohKardan/TallaEgg/issues/69) üretim URL'leri, [#70](https://github.com/MohKardan/TallaEgg/issues/70) süreç denetimi ve [#71](https://github.com/MohKardan/TallaEgg/issues/71) CI) tamamlanmıştır — gerçek adımlar için yukarıdaki [Üretim Dağıtımı](#production-deployment) bölümüne ve [`docs/operations/WINDOWS_DEPLOYMENT.md`](docs/operations/WINDOWS_DEPLOYMENT.md) belgesine bakın. `Production`, yalnızca Development değil, yerelde SQL Server Express'e karşı çalıştırılmış ve doğrulanmıştır — burada daha önce yer alan, hiç çalıştırılmadığına dair not artık doğru değildir.

Servisleri yayınlamak ve kurmak için `scripts/windows-services/` kullanın — artık depodaki tek yayın aracı budur. Kök dizindeki eski bir `publish-all.ps1` ve bir `publishes/` klasörü #69/#70 öncesine aitti ve kaldırıldı: dört yerine beş servis yayınlıyorlardı (#69'un dağıtılmamasına karar verdiği ikisi dahil), #69'un kaldırdığı HTTPS dinleme adreslerine hâlâ başvuruyorlardı ve denetlenen bir servis yerine elle `dotnet *.dll` başlatmayı varsayıyorlardı.
