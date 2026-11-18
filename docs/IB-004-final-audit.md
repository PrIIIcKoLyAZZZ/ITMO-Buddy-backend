# IB-004 — замечания к финальному аудиту

## Этап 3 — поиск и рекомендации

- Стандартный `IRecommendationSkipStore` хранит пропуски в памяти процесса. После перезапуска записи теряются, а между экземплярами приложения не синхронизируются.
- Если доступность кандидата меняется между ранжированием и чтением карточек, страница может оказаться короче, а общее число соответствует результату ранжирования.
- SQL-запросы, индексы и пакетный обход проверяются по исходному коду. Планы PostgreSQL и поведение на реальной базе в рамках IB-004 не проверяются.

## Этап 4 — объявления и отклики

- Конкурентное принятие за последнее место и откат связанных записей проверены статически. В финальном аудите сверить защиту через `xmin`, границы транзакции и отправку realtime после подтверждённого commit.
- Удаление объявления сохраняет созданные диалоги и переписку. При проверке основания общения учитывать, что исходный принятый отклик мог быть удалён вместе с объявлением.

## Этап 5 — лайки и блокировки

- После блокировки `UnlikeAsync` может вернуть `IsMatch = true` и `Match = null`: прежний мэтч сохраняется, а карточка и данные диалога недоступны.
- Во всех прямых взаимодействиях с записью согласованно использовать `IUserPairLock` внутри транзакции и проверять доступ после захвата блокировки. Сверить лайки, блокировки, отклики и сообщения.

## Этап 6 — согласованность прочтения

- Выборочная и массовая отметки уведомлений синхронизируют связанные входящие сообщения в той же транзакции. Принадлежность сверяется по получателю уведомления и участникам диалога. Ранее записанное время прочтения сохраняется, а новое время сообщения не может предшествовать его отправке.
- Чтение истории и отметка уведомлений используют общую транзакционную блокировку получателя. Оба пути обновляют сначала уведомления, затем сообщения. Захват блокировки пары, если она нужна, предшествует блокировке получателя.

## Этап 7 — итог статического аудита

### Реализация и структура

| Application Services | Проверенные сценарии |
| --- | --- |
| `IAuthService` | Регистрация с согласием, вход, ротация refresh и выход. Access token до 30 минут, refresh на 14 дней. Хранятся хэши. |
| `IProfileService` | Чтение профилей с проверкой доступа, полная замена редактируемых полей, загрузка JPEG/PNG до 5 МБ, физическое удаление учётной записи. |
| `IDictionaryService` | Факультеты, поиск интересов и навыков, нормализация и атомарное получение или создание тегов. |
| `IUserSearchService` | Текстовый поиск от двух символов, AND между группами, OR внутри каждой группы, пакетные карточки и пагинация по 20. |
| `IRecommendationService` | Глобальная оценка, причины, временные пропуски, пустая страница для неполной анкеты текущего пользователя. |
| `IListingService` | Создание, замена, удаление, закрытие, просмотр и публичная и личная ленты. |
| `IListingResponseService` | Отклик, принятие, отклонение, постраничные списки, сохранение диалога и уведомлений. |
| `ILikeService` | Лайк, удаление исходящего лайка, один мэтч для пары, переиспользование диалога. |
| `IBlockService` | Идемпотентная направленная блокировка и её снятие. |
| `IConversationService` | Список диалогов, история и отметки прочтения, отправка сообщений до 2000 символов. |
| `INotificationService` | Личная лента, счётчик непрочитанных, выборочная и массовая отметка с синхронизацией входящих сообщений. |

Все 40 методов соответствуют интерфейсам и принимают `CancellationToken`. Рабочие компоненты находятся в `Auth`, `Profiles`, `Dictionaries`, `Search`, `Recommendations`, `Listings`, `Interactions`, `Conversations`, `Notifications`, а общие — в `Common`, `Errors` и `Validation`.

В DI зарегистрированы валидаторы `RegisterRequest`, `LoginRequest`, `UpdateProfileRequest`, `ProfilePhotoUpload`, `SearchUsersRequest`, `GetRecommendationsRequest`, `CreateListingRequest`, `UpdateListingRequest`, `SearchListingsRequest`, `CreateListingResponseRequest`, `ReviewListingResponseRequest`, `SendMessageRequest`, `CreateDictionaryItemRequest`, `DictionarySearchRequest`, `MarkNotificationsReadRequest`, `PaginationRequest`. Методы с входными DTO вызывают `ValidateRequestAsync`. Решения по откликам принимают GUID, а валидатор DTO решения подготовлен для будущего API.

### Архитектура, доступ и транзакции

- Domain независим. Application ссылается на Domain, Contracts и Abstractions. Циклов, EF Core, `HttpContext` и зависимости Application от Infrastructure нет. Все 11 сервисов зарегистрированы как scoped. Persistence регистрирует репозитории, запросы и транзакции. Application предоставляет системные UTC-часы с возможностью переопределения.
- `CurrentUserGuard` и проверки принадлежности защищают операции от подстановки пользователя, автора, кандидата или отправителя. Проверены активность, блокировки в обе стороны, видимость карточек, контакт только владельцу и принадлежность уведомлений и диалогов. Вход и чтение справочников доступны без учётной записи. Создание пользовательских тегов требует активного аккаунта.
- Регистрация, анкета и refresh-сессия сохраняются атомарно. Отзыв старого refresh и выпуск нового используют одну транзакцию с `xmin`. Конкурентное принятие подтверждается версией объявления, а диалог, счётчик, отклик и уведомление откатываются вместе при конфликте.
- Лайки, блокировки, отклики и сообщения используют транзакционную блокировку пары. Уникальные индексы направленных лайков, мэтчей и диалогов защищают от дублей. Уникальный конфликт лайка или мэтча перечитывается после отката не более одного раза.
- Отметки прочтения сериализуются по получателю. Сначала берётся блокировка пары, если она нужна, затем блокировка прочтения. Оба пути обновляют уведомления перед сообщениями. Пакетный UPDATE ограничен получателем, проверяет участников и направление сообщения, сохраняет первое время прочтения. Массовая отметка охватывает весь набор.
- Удаление аккаунта очищает зависимые данные и корректирует счётчики транзакционно. Файловая очередь сохраняет задание на компенсацию при неопределённости или недоступности хранилища.
- `ApplicationErrorMapper` преобразует ожидаемые Domain/Persistence ошибки в Validation, Unauthorized, Forbidden, NotFound, Conflict и InternalError. Проверены типизированные unique, foreign key, check, not-null, concurrency, serialization и deadlock. Сбой связи при commit сохраняет отдельный неопределённый исход и не запускает автоматический повтор или realtime. Realtime публикуется после подтверждённого commit, PostgreSQL остаётся источником истины.

### Запросы и ранжирование

Публичные ленты и история используют стабильные сортировки и серверную пагинацию. Карточки пользователей, авторов и кандидатов читаются пакетно. Последнее сообщение и непрочитанные вычисляются на сервере без отдельных SQL-вызовов на диалог. Сверены PK, уникальные индексы пар и получателей, индексы лент, истории сообщений, непрочитанных, полнотекстового и триграммного поиска. Трансляция LINQ и пригодность индексов проверены по коду и документации провайдера. Реальные планы PostgreSQL не запускались.

Для рекомендаций применяются Жаккар для целей, интересов и навыков и академический показатель из совпадения факультетов и близости курса. Веса по умолчанию — 0.4, 0.3, 0.2, 0.1. Пустое объединение даёт ноль. SQL возвращает подходящих кандидатов пакетами по курсору, а Application оценивает весь набор. Очередь сохраняет только лучшие записи до конца запрошенной страницы. Итоговый порядок — Score DESC, UserId ASC. Полные карточки читаются только для выбранной страницы.

### Исправления по результатам аудита

- Вход перечитывает аккаунт под блокировкой перед сохранением активности и refresh-сессии. Это предотвращает обратный ход времени активности при конкурентных входах.
- Ошибка получения ссылки на фото при чтении профиля и карточек возвращает данные с пустой ссылкой. Отмена запроса сохраняет обычную семантику отмены.
- Известные отказы PostgreSQL на commit классифицируются по SQLSTATE. Неопределённость сохраняется для неподтверждённого исхода.
- Ранжирование удерживает только окно результатов, а операции над объявлениями загружают требуемые навыки или целевой отклик без материализации всей коллекции откликов.
- Создание пользовательских тегов закрыто для анонимных и неактивных пользователей. Зарегистрирована базовая реализация UTC-часов. Contracts и схема PostgreSQL на этапе 7 не менялись.

### Риски и ограничения

| Риск | Статус | Результат оценки |
| --- | --- | --- |
| Пропуски рекомендаций в памяти | Допустим для MVP | Теряются при перезапуске и локальны экземпляру. Хранилище можно переопределить в DI. |
| Неполная страница рекомендаций | Допустим для MVP | Повторная проверка видимости и блокировок при чтении карточек защищает приватность. При изменении доступности итог может быть короче. |
| Фотографии и сбой файлового хранилища | Исправлен | Необязательная ссылка не срывает выдачу. Для повторной очистки файлов всё ещё требуется обработчик IB-005. |
| Диалог после удаления объявления или отклика | Допустим для MVP | Сохраняются история и основание, зафиксированное при создании диалога. Доступ зависит от участников, активности и блокировок. |
| История откликов заблокированных пользователей | Допустим для MVP | Авторская выдача исключает заблокированного кандидата. Кандидату доступен собственный исторический отклик без карточки автора. Прямое взаимодействие проверяется заново. |
| `IsMatch = true` при `Match = null` | Допустим для MVP | Исторический мэтч сохраняется, данные недоступного собеседника не выдаются. |
| Потеря realtime-событий | Допустим для MVP | Доставка best-effort. История и счётчики восстанавливаются из PostgreSQL. |
| Повтор Send после неизвестного commit | Допустим для MVP | Автоматический повтор отсутствует. Повтор клиента может создать отдельное сообщение и уведомление. Для точной дедупликации потребуется ключ запроса в контракте. |
| Блокировки при конкурентных операциях | Исправлен | Прямые записи и отметки используют согласованный порядок. Межтранзакционные сценарии на реальной БД остаются непроверенными. |

Глубокие страницы рекомендаций всё ещё пропорциональны запрошенному окну, а точная оценка требует просмотра всех подходящих кандидатов. Проверка формата изображений и границ памяти опирается на управляемые декодеры. Анимированный PNG отклоняется.

### Граница готовности

IB-005/IB-006 должны предоставить конкретные регистрации `ICurrentUser`, `IPasswordHasher`, `ITokenService`, `IFileStorage`, `IRealtimeNotifier`, обработчик файловой очереди, композицию приложения и HTTP/SignalR endpoints. Сравнение времени ответа при неверных учётных данных зависит от реализации хешера и политики HTTP. Миграция `AccountFileCleanup` подготовлена на этапе 2 и требует применения при развёртывании. Тесты и сценарии с реальной инфраструктурой в IB-004 не выполнялись.

Рекомендация по приёмке — принять Application Layer по исходному коду и сборке. Готовность бэкенда к эксплуатации проверить после подключения провайдеров и применения миграции в рамках IB-005/IB-006.

### Сборка и ограничения проверки

`dotnet clean`, `dotnet restore`, `dotnet build --no-restore` и `git diff --check` завершились успешно. Собраны все семь проектов, 0 ошибок и 0 предупреждений. Unit, Integration и E2E не запускались. Миграция, конкурентные транзакции, запросы и доставка событий не выполнялись на реальной инфраструктуре.

### История этапов

| Этап | Коммиты |
| --- | --- |
| 1 | `8e59d0c9bd9dc16b3b25879eda21eaae94d2f5cf` |
| 2 | `08075bbc95b330f2ccabbf27775edfd1753ac60d`, `71b71519d4721afcb390eef594a90d6b93daa958` |
| 3 | `202651c83f77a257e587485d8632497f66431104` |
| 4 | `45582e2327c096d694c55ac5ea452195f38cc372` |
| 5 | `7870a0b7b0a287666ce80f8410dbc078df5d6b6d` |
| 6 | `56f065e77bee0b9866d5242cd6b20d56bfd5cab0`, `3b8ee64421c277bbc6de73193e902a464883da21` |
| 7 | Коммит, содержащий данный отчёт. |

### Файлы IB-004

В разделе перечислены изменения от базы `026ede80b47364148b06e712c848fb02fcd3caa6` по этап 7 включительно.

```text
Directory.Packages.props
docs/IB-004-final-audit.md
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IAccountMaintenanceRepository.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IConversationRepository.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IFileCleanupQueue.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IListingRepository.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IMessageRepository.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/INotificationRepository.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IReferenceDataRepository.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/IUserPairLock.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/Queries/IConversationQueries.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/Queries/IListingSearchQueries.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/Queries/IRecommendationCandidateQueries.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Persistence/Queries/IUserSearchQueries.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application.Abstractions/Storage/IFileStorage.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Auth/AuthService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Common/ContractEnum.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Common/CurrentUserGuard.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Common/OptionalPhotoUrl.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Common/Pagination.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Common/RealtimeDelivery.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Common/SystemDateTimeProvider.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Conversations/ConversationService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/DependencyInjection.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Dictionaries/DictionaryService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Errors/AppException.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Errors/ApplicationErrorMapper.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Errors/ApplicationOperation.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/ITMO.Buddy.Application.csproj
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Interactions/BlockService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Interactions/LikeService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Listings/ListingAccess.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Listings/ListingPresentation.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Listings/ListingResponseService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Listings/ListingService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Notifications/NotificationService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Notifications/UnreadNotificationDelivery.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Profiles/ProfilePhoto.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Profiles/ProfilePhotoValidator.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Profiles/ProfileService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Recommendations/MemoryRecommendationSkipStore.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Recommendations/RecommendationOptions.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Recommendations/RecommendationScorer.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Recommendations/RecommendationService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Search/ProfileCardMapper.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Search/UserSearchService.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Validation/AuthValidators.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Validation/ListingValidators.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Validation/OtherRequestValidators.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Validation/ProfileSearchValidators.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Validation/RequestValidation.cs
src/ITMO.Buddy/Application/ITMO.Buddy.Application/Validation/ValidationRules.cs
src/ITMO.Buddy/Domain/ITMO.Buddy.Domain/Listings/Listing.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Configurations/FileCleanupEntryConfiguration.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Configurations/RefreshTokenConfiguration.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/DependencyInjection.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Migrations/20261118142755_AccountFileCleanup.Designer.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Migrations/20261118142755_AccountFileCleanup.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Migrations/BuddyDbContextModelSnapshot.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Models/FileCleanupEntry.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Models/ProfileGoalsStore.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Queries/ConversationQueries.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Queries/DiscoverableProfiles.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Queries/ListingSearchQueries.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Queries/RecommendationCandidateQueries.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Queries/UserSearchQueries.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/AccountMaintenanceRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/ConversationRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/FileCleanupQueue.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/ListingRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/MessageRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/NotificationRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/ReferenceDataRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Repositories/UserRepository.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Transactions/BuddyTransactionRunner.cs
src/ITMO.Buddy/Infrastructure/ITMO.Buddy.Infrastructure.Persistence/Transactions/PostgresUserPairLock.cs
```
