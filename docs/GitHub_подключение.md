# GitHub и обновление проекта

Проект опубликован в приватном репозитории владельца: [ARTYR1387/preorder-store](https://github.com/ARTYR1387/preorder-store). Для чтения приватного репозитория аккаунту нужен доступ к GitHub-аккаунту владельца.

## Новый компьютер

```bash
git clone https://github.com/ARTYR1387/preorder-store.git
cd preorder-store
git pull origin main
dotnet restore PreOrderStore.sln
dotnet build PreOrderStore.sln
dotnet test PreOrderStore.sln
dotnet run --project PreOrderStore/PreOrderStore.csproj
```

## Уже клонированный проект

Откройте терминал в папке репозитория и выполните:

```bash
git pull origin main
```

После обновления схема локальной SQLite-базы меняется через включённые EF Core migrations при запуске. Удалять существующую базу вручную не нужно. Git не переносит личную SQLite-базу: новый компьютер создаст свою при первом запуске. Зарегистрируйте локальный аккаунт на новом устройстве.

## Проверки

```bash
dotnet build PreOrderStore.sln
dotnet test PreOrderStore.sln
```

Приватный репозиторий требует авторизации Git для чтения. Не добавляйте в проект пароли, персональные базы, платёжные данные или иные пользовательские секреты.
