using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Host=localhost;Port=5432;Database=clouddesk;Username=clouddesk;Password=clouddesk";

var dataSource = NpgsqlDataSource.Create(connectionString);

var app = builder.Build();

static bool IsValidStatus(string status) =>
    status is "new" or "in_progress" or "done";

static string? Validate(string? title, string? description, int priority)
{
    if (string.IsNullOrWhiteSpace(title) || title.Length < 3 || title.Length > 120)
        return "title должен содержать от 3 до 120 символов";
    if (string.IsNullOrWhiteSpace(description))
        return "description не может быть пустым";
    if (priority < 1 || priority > 3)
        return "priority должен быть от 1 до 3";
    return null;
}

app.MapGet("/", () => Results.Ok(new
{
    service = "CloudDesk",
    health = "/health",
    tickets = "/api/tickets",
    stats = "/api/stats"
}));

app.MapGet("/health", async () =>
{
    try
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT 1", conn);
        await cmd.ExecuteScalarAsync();
        return Results.Ok(new { status = "ok" });
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

app.MapGet("/api/tickets", async (string? status, int? priority) =>
{
    if (status is not null && !IsValidStatus(status))
        return Results.BadRequest(new { error = "status должен быть new, in_progress или done" });
    if (priority is not null && (priority < 1 || priority > 3))
        return Results.BadRequest(new { error = "priority должен быть от 1 до 3" });

    var conditions = new List<string>();
    if (status is not null) conditions.Add("status = @status");
    if (priority is not null) conditions.Add("priority = @priority");

    var sql = "SELECT id, title, description, priority, status, created_at FROM tickets";
    if (conditions.Count > 0) sql += " WHERE " + string.Join(" AND ", conditions);
    sql += " ORDER BY id";

    var list = new List<object>();
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(sql, conn);
    if (status is not null) cmd.Parameters.AddWithValue("status", status);
    if (priority is not null) cmd.Parameters.AddWithValue("priority", priority.Value);

    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        list.Add(new
        {
            id = reader.GetInt32(0),
            title = reader.GetString(1),
            description = reader.GetString(2),
            priority = reader.GetInt32(3),
            status = reader.GetString(4),
            createdAt = reader.GetDateTime(5)
        });
    }

    return Results.Ok(list);
});

app.MapGet("/api/tickets/{id:int}", async (int id) =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT id, title, description, priority, status, created_at FROM tickets WHERE id = @id", conn);
    cmd.Parameters.AddWithValue("id", id);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound(new { error = "Заявка не найдена" });

    return Results.Ok(new
    {
        id = reader.GetInt32(0),
        title = reader.GetString(1),
        description = reader.GetString(2),
        priority = reader.GetInt32(3),
        status = reader.GetString(4),
        createdAt = reader.GetDateTime(5)
    });
});

app.MapPost("/api/tickets", async (TicketInput input) =>
{
    var error = Validate(input.Title, input.Description, input.Priority);
    if (error is not null) return Results.BadRequest(new { error });

    var status = string.IsNullOrEmpty(input.Status) ? "new" : input.Status;
    if (!IsValidStatus(status))
        return Results.BadRequest(new { error = "status должен быть new, in_progress или done" });

    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "INSERT INTO tickets (title, description, priority, status) " +
        "VALUES (@title, @description, @priority, @status) RETURNING id, created_at", conn);
    cmd.Parameters.AddWithValue("title", input.Title!);
    cmd.Parameters.AddWithValue("description", input.Description!);
    cmd.Parameters.AddWithValue("priority", input.Priority);
    cmd.Parameters.AddWithValue("status", status);

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();
    var id = reader.GetInt32(0);
    var createdAt = reader.GetDateTime(1);

    return Results.Created($"/api/tickets/{id}", new
    {
        id,
        title = input.Title,
        description = input.Description,
        priority = input.Priority,
        status,
        createdAt
    });
});

app.MapPut("/api/tickets/{id:int}", async (int id, TicketInput input) =>
{
    var error = Validate(input.Title, input.Description, input.Priority);
    if (error is not null) return Results.BadRequest(new { error });

    if (string.IsNullOrEmpty(input.Status) || !IsValidStatus(input.Status))
        return Results.BadRequest(new { error = "status должен быть new, in_progress или done" });

    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "UPDATE tickets SET title = @title, description = @description, priority = @priority, status = @status " +
        "WHERE id = @id RETURNING id, created_at", conn);
    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("title", input.Title!);
    cmd.Parameters.AddWithValue("description", input.Description!);
    cmd.Parameters.AddWithValue("priority", input.Priority);
    cmd.Parameters.AddWithValue("status", input.Status);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.NotFound(new { error = "Заявка не найдена" });

    return Results.Ok(new
    {
        id,
        title = input.Title,
        description = input.Description,
        priority = input.Priority,
        status = input.Status,
        createdAt = reader.GetDateTime(1)
    });
});

app.MapDelete("/api/tickets/{id:int}", async (int id) =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand("DELETE FROM tickets WHERE id = @id", conn);
    cmd.Parameters.AddWithValue("id", id);

    var affected = await cmd.ExecuteNonQueryAsync();
    return affected == 0 ? Results.NotFound(new { error = "Заявка не найдена" }) : Results.NoContent();
});

app.MapGet("/api/stats", async () =>
{
    await using var conn = await dataSource.OpenConnectionAsync();
    await using var cmd = new NpgsqlCommand(
        "SELECT count(*) AS total, " +
        "count(*) FILTER (WHERE status = 'new') AS new_count, " +
        "count(*) FILTER (WHERE status = 'in_progress') AS in_progress_count, " +
        "count(*) FILTER (WHERE status = 'done') AS done_count " +
        "FROM tickets", conn);

    await using var reader = await cmd.ExecuteReaderAsync();
    await reader.ReadAsync();

    return Results.Ok(new
    {
        total = reader.GetInt64(0),
        @new = reader.GetInt64(1),
        in_progress = reader.GetInt64(2),
        done = reader.GetInt64(3)
    });
});

app.Run();

public record TicketInput(string? Title, string? Description, int Priority, string? Status);
