# CloudDesk

Сервис учёта заявок технической поддержки: ASP.NET Core Minimal API + PostgreSQL, запуск через Docker Compose.

## Запуск приложения

```
cp .env.example .env
```

При необходимости измените пароль в `.env`, затем:

```
docker compose up -d --build
```

Сервисы:

- `api` — приложение, доступно на http://localhost:8080 (в Codespaces — через вкладку Ports, порт 8080)
- `db` — PostgreSQL 16, наружу не публикуется, данные хранятся в томе `pgdata`

Проверка состояния:

```
docker compose ps
docker compose logs -f api
curl -i http://localhost:8080/health
```

Остановка:

```
docker compose down
```

## HTTP-маршруты

| Метод | Маршрут | Описание |
|-------|---------|----------|
| GET | `/api/tickets?status=new&priority=3` | Список заявок с фильтрацией по статусу и приоритету |
| GET | `/api/tickets/{id}` | Заявка по ID; 404, если не найдена |
| POST | `/api/tickets` | Создание заявки, ответ 201 и ID записи |
| PUT | `/api/tickets/{id}` | Изменение названия, описания, приоритета и статуса |
| DELETE | `/api/tickets/{id}` | Удаление заявки; 204 при успехе, 404 при отсутствии |
| GET | `/api/stats` | Общее число заявок и количество по каждому статусу |
| GET | `/health` | 200, если API работает и база доступна |

Тело запроса для POST и PUT:

```json
{
  "title": "Не работает мышь",
  "description": "Курсор не двигается",
  "priority": 2,
  "status": "new"
}
```

Правила: `title` — от 3 до 120 символов, `priority` — от 1 до 3, `status` — `new`, `in_progress` или `done`. При ошибках возвращается 400.

## Проверка сохранности данных

1. Создайте заявку и запомните её ID:

```
curl -i -X POST http://localhost:8080/api/tickets \
  -H "Content-Type: application/json" \
  -d '{"title":"Тестовая заявка","description":"Проверка сохранности","priority":1}'
```

2. Остановите сервисы без удаления томов и запустите снова:

```
docker compose down
docker compose up -d
curl -i http://localhost:8080/api/tickets/{id}
```

Заявка должна остаться на месте: данные лежат в томе `pgdata`, а не в контейнере.

3. Остановите только базу данных:

```
docker stop clouddesk-db-1
curl -i http://localhost:8080/health
```

`/health` вернёт 503, пока база недоступна. После `docker start clouddesk-db-1` API снова начнёт отвечать 200.

4. Полный сброс данных (удаляет том и все заявки):

```
docker compose down -v
```

После сброса при следующем запуске `init.sql` создаст таблицу и тестовые заявки заново.
