## k6 performance tests (load + stress reorder)

Цей каталог містить k6-скрипти для вимоги по перформанс-тестах:

- **Load**: `GET /api/songs?genre=...&artist=...`
- **Stress**: `PUT /api/playlists/{id}/reorder`

Нижче — повна інструкція запуску **для Windows PowerShell**.

## Підготовка: 2 термінали

Відкрий **2 термінали**:

- **Термінал A**: для API (працює весь час тесту).
- **Термінал B**: для k6 (звідси запускаєш навантаження).

## 1) Перейди в корінь проєкту

У **обох** терміналах:

```powershell
cd "C:\Users\vladd\4_course\2_semester\Software testing\project"
```

Перевір, що папка `k6` існує:

```powershell
dir .\k6
```

## 2) Підніми PostgreSQL через docker compose

У будь-якому терміналі:

```powershell
docker compose up -d
```

Переконайся, що контейнер живий:

```powershell
docker compose ps
```

Очікування: сервіс **postgres** має бути **running**.

## 3) Запусти API з підключенням до Postgres + seed (≥10k рядків)

У **Терміналі A**.

### 3.1. Встанови connection string

```powershell
$env:ConnectionStrings__Default="Host=localhost;Port=5432;Database=musicplaylist;Username=musicplaylist;Password=LocalDev_ChangeMe"
```

### 3.2. Увімкни наповнення БД

```powershell
$env:SEED_ON_STARTUP="true"
```

### 3.3. Запусти API

```powershell
dotnet run --project .\MusicPlaylist.Api\MusicPlaylist.Api.csproj
```

### 3.4. Знайди реальний URL/порт

У виводі буде щось типу:

```text
Now listening on: http://localhost:5067
```

Запам’ятай цей порт. Далі я буду писати його як `<PORT>`.

## 4) Перевір, що API реально доступний

У **Терміналі B**:

```powershell
$baseUrl="http://localhost:<PORT>"
curl "$baseUrl/api/songs"
```

Очікування: **200 OK** і JSON-масив (може бути великий).

## Запуск k6

## 5) Load тест: GET /api/songs?genre=...&artist=...

У **Терміналі B** задай `BASE_URL`:

```powershell
$env:BASE_URL="http://localhost:<PORT>"
```

### 5.1. Запуск “як є” (дефолт)

```powershell
k6 run .\k6\songs_load.js
```

### 5.2. Запуск з параметрами фільтра (genre/artist)

```powershell
$env:GENRE="Rock"
$env:ARTIST="k6-artist"
k6 run .\k6\songs_load.js
```

### 5.3. Зміна навантаження

- **VUS** — кількість віртуальних користувачів
- **DURATION** — тривалість тесту

Приклад:

```powershell
$env:VUS="50"
$env:DURATION="2m"
k6 run .\k6\songs_load.js
```

### 5.4. Пороги (thresholds), на що дивитись

Скрипт перевіряє:

- `http_req_failed` має бути низький (майже 0)
- `http_5xx` має бути майже 0
- `p(95)` по часу відповіді має бути нижче порога

Можеш задати свої пороги:

```powershell
$env:P95_MS="200"
$env:MAX_FAILED_RATE="0.01"
$env:MAX_5XX_RATE="0.001"
k6 run .\k6\songs_load.js
```

### 5.5. Як інтерпретувати результат

У кінці дивись на блоки:

- **THRESHOLDS**: мають бути `✓`
- **http_req_failed**: ~0%
- **http_req_duration p(95)**: наприклад 30–100ms (залежить від ПК/БД/обсягу)

## 6) Stress тест: PUT /api/playlists/{id}/reorder

У **Терміналі B**:

```powershell
$env:BASE_URL="http://localhost:<PORT>"
k6 run .\k6\playlist_reorder_stress.js
```

### 6.1. Що робить скрипт

У `setup()`:

- створює набір пісень
- створює кілька плейлистів і додає туди пісні
- потім під навантаженням викликає `PUT /api/playlists/{id}/reorder` з валідним body

### 6.2. Стадії stress (як ростуть VU)

За замовчуванням: `30s:5,30s:10,30s:20,30s:30,30s:40`

Можеш задати коротший прогін:

```powershell
$env:STAGES="10s:5,10s:10,10s:20"
k6 run .\k6\playlist_reorder_stress.js
```

### 6.3. Кількість плейлистів / пісень для setup

- **PLAYLISTS** — скільки плейлистів підготувати (щоб VU не били один і той самий)
- **SONG_COUNT** — скільки пісень у кожному плейлисті

Приклад:

```powershell
$env:PLAYLISTS="40"
$env:SONG_COUNT="20"
k6 run .\k6\playlist_reorder_stress.js
```

### 6.4. На що дивитись у результаті stress

Вимога для stress (практично):

- сервер **не падає**
- масових **500** бути не повинно
- допускаються **400/409** (валідація/конкурентність)

Тобто:

- `http_5xx` має бути близький до 0
- якщо є проблеми — дивись, чи це 409/400 (прийнятно) чи саме 500 (погано)

### 6.5. Якщо бачиш `connectex ... refused`

Це означає, що API не запущений або порт інший.

- перевір у **Терміналі A** `Now listening on: ...`
- онови `$env:BASE_URL` на правильний порт

## 7) Часті проблеми і швидкі рішення

### k6 ходить на `localhost:5000`, хоча API на `:5067`

```powershell
$env:BASE_URL="http://localhost:5067"
```

### `http_req_failed = 100%` і `actively refused`

99% означає, що API недоступний. Перевір:

- API справді запущений?
- порт правильний?
- firewall/antivirus не блокує?

### Дуже повільно / великий p95

Спробуй:

- менше `VUS`
- довше `DURATION` (щоб “прогріти” кеш)
- перевір, що БД на SSD, і Docker має достатньо RAM/CPU

## 8) Як красиво “здати” результат (мінімум)

- **Load**: показати, що `http_req_failed ~ 0`, `http_5xx ~ 0`, `p95` в межах порога.
- **Stress reorder**: показати, що сервер живий і **нема масових 500** (навіть якщо є 409/400).
