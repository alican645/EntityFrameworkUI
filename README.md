# EF Core Terminal UI (`efui`)

EF Core migration işlemlerini yön tuşlarıyla yapan terminal arayüzü. `dotnet ef ...` komutlarını elle yazmana gerek kalmaz.

## Kurulum

```bash
dotnet tool install --global EFCoreTerminalUI
```

Güncellemek için:

```bash
dotnet tool update --global EFCoreTerminalUI
```

Kaynaktan kurmak için:

```bash
cd EFCoreTerminalUI
dotnet pack -c Release
dotnet tool install --global --add-source ./nupkg EFCoreTerminalUI
```

## Kullanım

Solution klasöründe:

```bash
efui            # bulunduğun klasör
efui ../Proje   # başka bir klasör
```

1. İlk açılışta **migration projesi** (DbContext'in olduğu, ör. `Infrastructure`) ve **startup projesi** (ör. `WebApi`) seçilir. Önerilen proje en üstte gelir.
2. Birden fazla DbContext varsa listeden seçersin.
3. Seçimler `.efui.json` dosyasına kaydedilir, sonraki açılışlarda tekrar sorulmaz (değiştirmek için menüde *Proje / DbContext değiştir*).

### Menü

| İşlem | Karşılığı |
|---|---|
| Add Migration | `migrations add <ad>` (istersen hemen `database update`) |
| Remove Last Migration | `migrations remove`; son migration uygulanmışsa veritabanından geri alıp siler (`--force`) |
| Update Database (son migration) | `database update` |
| Update Database (migration seç / geri al) | `database update <migration>` veya `0` |
| SQL Script oluştur | `migrations script <from> <to> [--idempotent] -o dosya.sql` |
| Drop Database | `database drop --force` (onay için `DROP` yazılır) |

Migration listesinde uygulananlar yeşil ✔, bekleyenler sarı ● ile gösterilir. Veritabanına bağlanılamazsa liste bağlantısız okunur ve durum `?` olarak görünür.

## Gereksinimler

- .NET 9 veya üzeri
- `dotnet-ef` (yoksa açılışta yüklemeyi teklif eder)
- Startup projesinde `Microsoft.EntityFrameworkCore.Design` paketi

## Lisans

MIT. Topluluk aracıdır; Microsoft ile bağlantısı yoktur.
