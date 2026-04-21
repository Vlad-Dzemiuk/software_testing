# MusicPlaylist (Task 9)

Сервіс потокової музики з піснями та плейлистами користувачів.

## Стек

- **.NET 8**
- **ASP.NET Core** (Minimal API)
- **EF Core**
- **PostgreSQL** (через Docker Compose)
- **Тестування**: xUnit, Moq, AutoFixture, Testcontainers (для DB), k6 (перформанс)

## Сутності

- **Song**: Id, Title, Artist, Album, DurationSeconds, Genre, ReleaseDate  
- **Playlist**: Id, Name, Description, UserId, CreatedAt, IsPublic  
- **PlaylistSong**: Id, PlaylistId, SongId, Position, AddedAt  

## Ендпоінти API

| Метод | Маршрут | Опис |
|---|---|---|
| GET | `/api/songs` | Отримати всі пісні (фільтр за жанром, виконавцем) |
| POST | `/api/songs` | Додати пісню |
| GET | `/api/playlists` | Отримати плейлисти користувача |
| POST | `/api/playlists` | Створити плейлист |
| PUT | `/api/playlists/{id}` | Оновити деталі плейлиста |
| DELETE | `/api/playlists/{id}` | Видалити плейлист |
| POST | `/api/playlists/{id}/songs` | Додати пісню до плейлиста |
| DELETE | `/api/playlists/{id}/songs/{songId}` | Видалити пісню з плейлиста |
| PUT | `/api/playlists/{id}/reorder` | Змінити порядок пісень у плейлисті |

## Бізнес-правила

- Пісню не можна додати до одного плейлиста двічі
- Назва плейлиста має бути унікальною для кожного користувача
- Позиції мають бути послідовними (1, 2, 3...)
- Максимум **100** пісень у плейлисті

## Структура репозиторію (основне)

- `MusicPlaylist.Api/` — HTTP API
- `MusicPlaylist.Domain/` — доменні сутності
- `MusicPlaylist.Application/` — бізнес-логіка/сервіси/валідації
- `MusicPlaylist.Infrastructure/` — доступ до даних/EF Core/сідинг
- `MusicPlaylist.Application.Tests/` — **модульні тести** (ізольована логіка)
- `MusicPlaylist.IntegrationTests/` — **інтеграційні тести** (WebApplicationFactory) *(якщо присутній у гілці)*
- `MusicPlaylist.DatabaseTests/` — **DB тести** (Testcontainers + PostgreSQL) *(якщо присутній у гілці)*
- `k6/` — **перформанс-тести** (k6) *(якщо присутній у гілці)*

## Ініціалізація проєкту після `git clone` (Windows PowerShell)

Нижче — “правильний” порядок дій, щоб з нуля підняти проєкт локально після клонування репозиторію.

### 0) Передумови

- **.NET SDK 8.0**
- **Docker Desktop** (для PostgreSQL)
- (опційно) **k6** для перформанс-тестів

### 1) Клонування та перехід у папку репозиторію

```powershell
git clone <REPO_URL>
cd <REPO_FOLDER>
```

Далі всі команди виконуються **з кореня репозиторію** (там де `MusicPlaylist.sln`).

### 2) Відновлення залежностей та збірка

```powershell
dotnet restore
dotnet build
```

### 3) Підняти PostgreSQL (Docker Compose)

```powershell
docker compose up -d
docker compose ps
```

Очікування: сервіс `postgres` має бути `running`.

### 4) Запуск API з підключенням до Postgres + seed (≥10k)

> Наповнення БД потрібно для інтеграційних/перформанс тестів: **щонайменше 10 000 записів**, розподілених між усіма сутностями.

У цьому проєкті можна задати налаштування через змінні середовища:

```powershell
$env:ConnectionStrings__Default="Host=localhost;Port=5432;Database=musicplaylist;Username=musicplaylist;Password=LocalDev_ChangeMe"
$env:SEED_ON_STARTUP="true"
dotnet run --project .\MusicPlaylist.Api\MusicPlaylist.Api.csproj
```

У консолі буде щось типу:

```text
Now listening on: http://localhost:<PORT>
```

### 5) Перевірити, що API реально доступний

```powershell
$baseUrl="http://localhost:<PORT>"
curl "$baseUrl/api/songs"
```

Очікування: **200 OK** і JSON-масив.

### 6) Swagger (опційно)

Відкрий у браузері:

- `http://localhost:<PORT>/swagger`

## Тестування

### Модульні тести (Unit tests)

Мета: ізольовано покрити логіку, яку можна тестувати без HTTP/EF:

- валідація reorder (пермутація позицій, дублі, unknown songId)
- виявлення дублікатів пісень (на рівні сервісу до БД)
- максимум 100 пісень (на рівні сервісу)

Запуск:

```powershell
dotnet test .\MusicPlaylist.Application.Tests\MusicPlaylist.Application.Tests.csproj
```

### Інтеграційні тести (WebApplicationFactory)

Покриття: CRUD плейлистів, додавання/видалення пісень, endpoint reorder.

```powershell
dotnet test .\MusicPlaylist.IntegrationTests\MusicPlaylist.IntegrationTests.csproj
```

*(якщо проєкт `MusicPlaylist.IntegrationTests` є у твоїй гілці)*

### Тести бази даних (Testcontainers)

Покриття: унікальні обмеження, цілісність позицій після видалення, каскадне видалення.

```powershell
dotnet test .\MusicPlaylist.DatabaseTests\MusicPlaylist.DatabaseTests.csproj
```

*(якщо проєкт `MusicPlaylist.DatabaseTests` є у твоїй гілці)*

### Перформанс-тести (k6)

Покриття:

- Load: список пісень з фільтрами
- Stress: reorder плейлиста (допустимі 400/409; **масових 500 бути не повинно**)

Детальна інструкція: `k6/README.md`.

## AutoFixture (правило з Task.md)

AutoFixture використовується для генерації **некритичних** полів (лише валідні дані).

- **Song**: Title, Artist, Album, DurationSeconds, ReleaseDate  
- **Playlist**: Name, Description, CreatedAt  
- **PlaylistSong**: AddedAt  

Поля з бізнес-правилами потрібно задавати **явно** в тестах:

`Genre`, `UserId`, `IsPublic`, `Position`, `PlaylistId`, `SongId`.

## CI (GitHub Actions)

У репозиторії налаштований CI, який запускає тести на кожен `push` та `pull_request`:

- `.github/workflows/ci.yml` виконує `dotnet restore`, `dotnet build`, `dotnet test`.

