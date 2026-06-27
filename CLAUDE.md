# Forex — Business Management System

Oyoq kiyim zavodi uchun biznes boshqaruv tizimi (savdo, ta'minot, moliya, ishlab chiqarish qoldig'i).

## Arxitektura

Clean Architecture + CQRS (MediatR).

- `src/backend/Forex.Domain` — Entity'lar, Enum'lar (hech narsaga bog'liq emas)
- `src/backend/Forex.Application` — Use-case'lar: `Features/<Soha>/Commands` va `Queries`, DTO, Mapper
- `src/backend/Forex.Infrastructure` — EF Core (`Persistence/AppDbContext`), MinIO, background, security
- `src/backend/Forex.WebApi` — REST API controller'lar (`/scalar/v1` da hujjat)
- `src/frontend/Forex.ClientService` — API bilan ishlaydigan HTTP klient (`IApi*` interfeyslar)
- `src/frontend/Forex.Wpf` — WPF desktop ilova (MVVM, CommunityToolkit.Mvvm)
- `src/Forex.AppHost` — .NET Aspire orkestratsiya

Texnologiyalar: .NET 9, PostgreSQL, MinIO, JWT, Mapster/AutoMapper, ImageSharp.

## Build va ishga tushirish

```bash
dotnet build Forex.sln
```

Lokal ishga tushirish uchun README.md va DOCKER.md ga qarang.

## Kod uslubi

- Professional, **izohsiz** (kommentariyasiz), sodda va kam kod. Yangi izoh qo'shilmaydi.
- Mavjud naqshga amal qilish: yangi funksiya = `Features/<Soha>/Commands/<Ism>Command.cs` + Handler, controller MediatR orqali chaqiradi.
- Frontend MVVM: `[ObservableProperty]`, `[RelayCommand]`.

## Faol bo'limlar

Savdo (Sales), To'lov (Transactions), Mahsulot (Products), Foydalanuvchi (Users),
Hisobot (Reports), Sozlamalar (Settings), Ta'minot (Supply).

## Ta'minot (Supply) mantig'i

Sodda moliyaviy hisob: `Date`, `PartyType` (Ta'minotchi/Vositachi), `Amount`, `Description`,
`UserId`, `CurrencyId`. Mahsulot/miqdor/kontiner maydonlari YO'Q — tafsilot izohga yoziladi.

- **Ta'minotchi**: yarim tayyor mahsulot sotib olingani uchun to'lov → balansга `Amount` qo'shiladi.
- **Vositachi**: Xitoydan konteyner tashigani uchun haq (~$1000/konteyner) → balansга `Amount` qo'shiladi.

## Server URL

Klient server URL'ini `%LocalAppData%\ForexApp\settings.json` da saqlaydi. Settings'da yoki
login paytida bog'lanmasa ochiladigan `ServerUrlWindow` orqali o'zgartiriladi (test → saqlash → qayta login).

## Ma'lumotlar bazasi

Production PostgreSQL. EF migratsiyalar va qo'lda SQL skriptlar `docs/database/` da.
Production DB'ga to'g'ridan-to'g'ri tegilmaydi — skript beriladi, foydalanuvchi o'zi ishga tushiradi.
