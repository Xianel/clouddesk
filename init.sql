CREATE TABLE tickets (
    id SERIAL PRIMARY KEY,
    title VARCHAR(120) NOT NULL,
    description TEXT NOT NULL,
    priority INTEGER NOT NULL CHECK (priority BETWEEN 1 AND 3),
    status VARCHAR(20) NOT NULL DEFAULT 'new' CHECK (status IN ('new', 'in_progress', 'done')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO tickets (title, description, priority, status, created_at) VALUES
    ('Не включается ноутбук', 'При нажатии кнопки питания индикатор не загорается.', 3, 'in_progress', now() - interval '6 days'),
    ('Нужен доступ к 1С', 'Сотруднику бухгалтерии требуется доступ к базе 1С.', 2, 'new', now() - interval '5 days'),
    ('Не печатает принтер', 'Принтер в кабинете 204 выдаёт ошибку подачи бумаги.', 2, 'done', now() - interval '4 days'),
    ('Установить офисный пакет', 'Установить LibreOffice на новый компьютер.', 1, 'done', now() - interval '3 days'),
    ('Не работает корпоративная почта', 'Почтовый клиент не синхронизирует письма с утра.', 3, 'new', now() - interval '2 days'),
    ('Медленно работает сеть', 'В кабинете 310 низкая скорость интернета.', 1, 'in_progress', now() - interval '1 day');
