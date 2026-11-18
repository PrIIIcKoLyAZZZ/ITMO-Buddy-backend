# IB-006 — Final Integration & Static Audit

## 1. Executive Summary

ITMO Buddy Backend MVP реализует 35 REST-операций, JWT-аутентификацию, refresh-сессии, переписку, пять адресных SignalR-событий и эксплуатационные endpoints. Финальный аудит повторил сквозные сценарии на отдельной PostgreSQL 17 и MinIO с HTTPS. Рекомендация для приёмки MVP — **ACCEPT WITH LIMITATIONS**. Эксплуатационная готовность к публичному развёртыванию не подтверждена.

Обнаружен воспроизводимый временной канал в существующем входе. Он документирован в разделе 10. Исправление затрагивает ранее принятый Application и требует отдельного согласования. В этапе 7 исходный код не менялся.

## 2. Architecture Overview

| Проект | Назначение | Прямые зависимости внутри решения |
|---|---|---|
| Domain | Сущности, инварианты и перечисления | — |
| Application.Contracts | Публичные DTO, ошибки и пагинация | — |
| Application.Abstractions | Порты сервисов, хранилищ, realtime и безопасности | Domain, Contracts |
| Application | Предметные сценарии, валидация, транзакционная координация | Domain, Contracts, Abstractions |
| Infrastructure.Persistence | EF Core, PostgreSQL, запросы, репозитории и миграции | Domain, Abstractions |
| Infrastructure | JWT issuance, BCrypt, MinIO, фоновый FileCleanup | Domain, Abstractions |
| Api | Composition Root, HTTP, SignalR и эксплуатационные адаптеры | Application, Contracts, Abstractions, оба инфраструктурных проекта |

Граф `ProjectReference` ацикличен. Внутренние проекты не ссылаются на API. Версии пакетов задаёт `Directory.Packages.props` через Central Package Management. Контроллеры используют Application-интерфейсы. Предметные инварианты, SQL, доступ к данным и создание уведомлений остались в соответствующих слоях.

## 3. Implemented REST API

| Контроллер | Метод | Маршрут | Вход | Успех / ответ |
|---|---|---|---|---|
| Auth | POST | `/api/auth/register` | RegisterRequest | 201 / AuthResponse |
| Auth | POST | `/api/auth/login` | LoginRequest | 200 / AuthResponse |
| Auth | POST | `/api/auth/refresh` | refresh-cookie | 200 / AuthResponse |
| Auth | POST | `/api/auth/logout` | refresh-cookie, если есть | 204 |
| Profiles | GET | `/api/profiles/me` | — | 200 / ProfileResponse |
| Profiles | PUT | `/api/profiles/me` | UpdateProfileRequest | 200 / ProfileResponse |
| Profiles | GET | `/api/profiles/{id:guid}` | UserId | 200 / ProfileResponse |
| Profiles | POST | `/api/profiles/me/photo` | multipart `file` | 200 / UploadPhotoResponse |
| Users | DELETE | `/api/users/me` | — | 204 |
| Dictionaries | GET | `/api/dictionaries/{type}` | DictionarySearchRequest для skills/interests | 200 / DictionaryItemResponse[] |
| Dictionaries | POST | `/api/dictionaries/interests` | CreateDictionaryItemRequest | 200 / DictionaryItemResponse |
| Dictionaries | POST | `/api/dictionaries/skills` | CreateDictionaryItemRequest | 200 / DictionaryItemResponse |
| Search | GET | `/api/search/users` | SearchUsersRequest, query | 200 / PagedResult&lt;ProfileCardResponse&gt; |
| Recommendations | GET | `/api/recommendations` | GetRecommendationsRequest, query | 200 / PagedResult&lt;RecommendationResponse&gt; |
| Recommendations | POST | `/api/recommendations/{userId:guid}/skip` | целевой UserId | 204 |
| Listings | GET | `/api/listings` | SearchListingsRequest, query | 200 / PagedResult&lt;ListingCardResponse&gt; |
| Listings | GET | `/api/listings/my` | PaginationRequest | 200 / PagedResult&lt;ListingCardResponse&gt; |
| Listings | POST | `/api/listings` | CreateListingRequest | 201 / ListingResponse и Location |
| Listings | GET | `/api/listings/{id:guid}` | ListingId | 200 / ListingResponse |
| Listings | PUT | `/api/listings/{id:guid}` | UpdateListingRequest | 200 / ListingResponse |
| Listings | DELETE | `/api/listings/{id:guid}` | ListingId | 204 |
| Listings | POST | `/api/listings/{id:guid}/close` | ListingId | 204 |
| Listings | GET | `/api/listings/{id:guid}/responses` | PaginationRequest | 200 / PagedResult&lt;ListingResponseDto&gt; |
| Listings | POST | `/api/listings/{id:guid}/responses` | CreateListingResponseRequest | 200 / ListingResponseDto |
| Responses | PATCH | `/api/responses/{id:guid}` | ReviewListingResponseRequest | 200 / ListingResponseDto |
| Responses | GET | `/api/responses/my` | PaginationRequest | 200 / PagedResult&lt;ListingResponseDto&gt; |
| Likes | POST | `/api/likes/{userId:guid}` | целевой UserId | 200 / LikeResponse |
| Likes | DELETE | `/api/likes/{userId:guid}` | целевой UserId | 200 / LikeResponse |
| Blocks | POST | `/api/blocks/{userId:guid}` | целевой UserId | 204 |
| Blocks | DELETE | `/api/blocks/{userId:guid}` | целевой UserId | 204 |
| Conversations | GET | `/api/conversations` | PaginationRequest | 200 / PagedResult&lt;ConversationResponse&gt; |
| Conversations | GET | `/api/conversations/{id:guid}/messages` | PaginationRequest | 200 / PagedResult&lt;MessageResponse&gt; |
| Conversations | POST | `/api/conversations/{id:guid}/messages` | SendMessageRequest | 200 / MessageResponse |
| Notifications | GET | `/api/notifications` | PaginationRequest | 200 / NotificationsPageResponse |
| Notifications | POST | `/api/notifications/read` | MarkNotificationsReadRequest | 204 |

Четыре auth-операции анонимны и требуют допустимого `Origin`. Остальные 31 требуют Bearer через fallback policy. GUID route constraints разделяют идентификаторы и литералы `me`/`my`. Query-коллекции MVC принимает как повторяющиеся ключи. Размер страницы фиксирован на 20. Контроллеры передают `CancellationToken` в Application. Для ошибок используются существующие коды и `ValidationErrors`.

## 4. Authentication & Authorization

`JwtOptions` задаёт общий источник issuer, audience и Base64-ключа для выпуска и валидации. Минимум 32 декодированных байта. Bearer требует HS256, проверяет подпись, issuer, audience, `exp` и `nbf` с нулевым `ClockSkew`. `MapInboundClaims=false`. Проверяются ровно один строковый `sub` с непустым GUID и ровно одна строковая роль `Student` или `Administrator`. Проверка исходного уже верифицированного payload выявляет неоднозначные claims, которые могли бы схлопнуться при материализации. `HttpCurrentUser` читает аутентифицированный principal. Идентификатор запроса, заголовок или тело не могут заменить его.

Финальный HTTPS smoke дал `401` для просроченного токена, будущего `nbf`, неверной подписи, issuer, audience, `sub`, роли, HS384 и unsigned JWT. Роль Administrator прошла на действующем локальном аккаунте. Bearer challenge сохранил `WWW-Authenticate: Bearer`. Проверка дублированных JWT claims проведена на этапе 1, код валидатора сохранился.

Refresh хранится в host-only cookie `itmo_buddy_refresh` с `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth` и сроком из результата сессии. Register, login и refresh возвращают только `AuthResponse`. Повтор старого refresh после ротации получил `401`. Logout с истёкшим access JWT и повторный logout получили `204`. Удаление одноразового аккаунта выдало истёкшую cookie с тем же Path. Logout отзывает refresh-сессию. Выданный access JWT сохраняет свой срок действия.

Origin guard допускает same-origin и настроенный frontend origin. Отсутствующий, `null`, дублированный и посторонний Origin получили `403`. Не браузерные клиенты auth также передают Origin. Для браузерной refresh-cookie frontend и API должны быть на одном site.

## 5. Application Integration

**DI Verified** в фактических ASP.NET Core request scopes. В каждом случае маршруты прошли создание контроллера и вызов зарегистрированного сервиса.

| Интерфейс | Реально вызванный сценарий |
|---|---|
| IAuthService | Register, login, refresh и logout |
| IProfileService | Профиль, загрузка фото и удаление аккаунта |
| IDictionaryService | Список, поиск и создание тегов |
| IUserSearchService | Поиск с комбинацией фильтров |
| IRecommendationService | Рейтинг и пропуск |
| IListingService | CRUD, лента, свои объявления и close |
| IListingResponseService | Создание, списки, Accept и Reject |
| ILikeService | Like, mutual match и Unlike |
| IBlockService | Block и Unblock |
| IConversationService | Список, история и REST-отправка |
| INotificationService | Список и выборочная/массовая отметка |

Все 11 сервисов, validators, `ICurrentUser`, `BuddyDbContext`, `ITransactionRunner`, репозитории и query services имеют scoped-границы. JWT provider, BCrypt, MinIO, `IRealtimeNotifier` и `IUserIdProvider` зарегистрированы singleton. SignalR notifier использует `IHubContext<ChatHub>`. Фоновые сервисы не захватывают scoped-объекты в конструкторе. FileCleanup создаёт собственный scope на операцию. Реальные маршруты задействовали подпись токена, BCrypt, PostgreSQL, MinIO и доставку SignalR. `ValidateOnBuild` и `ValidateScopes` включены. Ручных `BuildServiceProvider()` в коде нет. Конфигурация JWT, MinIO и FileCleanup валидируется при запуске.

## 6. Persistence

PostgreSQL 17 применил обе миграции в новой БД. `dotnet ef migrations list` и таблица истории подтвердили `20261117192121_InitialPersistence` и `20261118142755_AccountFileCleanup`. `has-pending-model-changes` не обнаружил расхождений. Схема содержит 35 check constraints, 24 foreign keys, 20 PK-ограничений, 69 индексов в public и таблицу `file_cleanup_queue`. Статически сверены уникальность email, refresh hash и пар, индексы для поиска и истории, PostgreSQL `xmin` и advisory locks. Хост не вызывает `EnsureCreated` и не применяет миграции автоматически.

При конкуренции за последнее место PostgreSQL и mapper сохраняют прикладной `409`. Данные и уведомления записываются в той же транзакции. Realtime инициируется после commit. Этапы 4 и 7 воспроизвели два одновременно ожидающих PATCH, один `200` и один `409 persistence.concurrent_change`, один принятый отклик при вместимости 1.

## 7. Business Flows

- **Profiles и Search.** PUT заменяет редактируемые поля целиком. Проверены видимость, исключение собственного профиля, блокировки, query-массивы и AND/OR. Цели и теги сохранились.
- **Recommendations.** Проверены Score, Reasons, фильтр целей, пустая выдача для незавершённого профиля и Skip. В изолированную БД добавлены 205 фиктивных кандидатов при внутреннем лимите 200. Страницы 1 и 10 вернули по 20, страница 11 — 5, страница 12 — 0. Пересечений ID между проверенными непустыми страницами нет. Курсор через границу пачки подтверждён.
- **Listings и Responses.** Повторены создание, GET по Location, обновление, лента, свои объявления, close и удаление. Подтверждены отклики, дубликат `409`, списки, Accept с нормализацией регистра и пробелов и Reject. Авторизацию владельца, отклонение чужого review и идемпотентность операций сверили с результатами этапов 3–4 и исходниками.
- **Likes и Blocks.** Повторены повторный Like, mutual match, Unlike и повторный Unlike. После Unlike диалог остался доступен. При встречных блокировках снять нужно обе записи, затем отправка восстанавливается. API самостоятельно не создаёт UserMatch, Conversation и Notification.

## 8. Conversations & Notifications

Доступ к переписке определяется участием и состоянием пользователей. REST сохраняет отправителя из `ICurrentUser`. Подставленный клиентом SenderId не повлиял на результат. Подтверждены ограничение 2000 символов, отказ для чужого диалога и блокировок в обоих направлениях. GET истории участником заполнил `ReadAtUtc`, изменил связанное уведомление и вернул `Cache-Control: no-store`. Уведомления поддерживают `UnreadCount`, выборочную и массовую отметку. Выборочно отметили реальный ID, некорректный GUID вернул `400`. История и уведомления на двух непустых страницах дополнительно проверены на этапе 5.

## 9. SignalR

`/hubs/chat` защищён Bearer. `IUserIdProvider` получает проверенный `sub` и форматирует его в GUID `D`, как `Clients.User`. Query `access_token` ограничен transport endpoint. REST и negotiate отклонили query-only JWT. В Hub отсутствуют методы отправки и подписки на чужую идентичность. Отсутствуют `Clients.All` для приватных данных и отдельная система доставки.

Официальный клиент Microsoft повторно подтвердил WebSocket и SSE. Два подключения A получили события A, B — свои, C — ни одного приватного события. При взаимном лайке оба участника получили свои события.

| Событие | Проверенный получатель |
|---|---|
| MessageReceived | Получатель REST-сообщения |
| MatchCreated | Оба участника мэтча |
| ListingResponseCreated | Автор объявления |
| ListingResponseReviewed | Кандидат по отклику |
| NotificationUnreadCountChanged | Владелец счётчика |

`CloseOnAuthenticationExpiration` закрыл реально открытое соединение после `exp`. Старый JWT получил `401`, новый JWT восстановил подключение и получение события. Оффлайн-сообщение сохранилось и после reconnect найдено ровно один раз в истории и уведомлениях. Клиентская доставка во время отключения не воспроизводится автоматически, источником истории остаётся REST.

## 10. Security

Middleware выполняет фильтрацию Host и доверенное forwarding до HTTPS и Origin-проверок. Далее идут обработка ошибок, routing, Hub Origin, CORS, JWT Authentication, Rate Limiting и Authorization. При пустой конфигурации forwarding отключён. Production отказался стартовать с пустым AllowedHosts, неверным ключом JWT и HTTP external endpoint MinIO. В явной конфигурации принимаются согласованные XFF и XFP от `KnownProxies` или `KnownNetworks` с `ForwardLimit`. XFH не используется. IP нормализуется для IPv4-mapped IPv6. Поддельный Host дал `400`, недоверенный XFP не отменил `307`. CORS допускает credentials только для явно настроенных origins. Корректность редиректа за TLS proxy, HSTS 30 дней и разделение IPv4/IPv6 проверялись на принятом коммите этапа 6.

Rate Limiting использует FIFO timestamps и монотонное время, сериализуя на IP весь интервал проверки и фиксации результата `LoginAsync`. Пять `auth.invalid_credentials` блокируют шестой вызов до Application, успех не очищает и не пополняет счётчик. Отмена и инфраструктурные ошибки не считаются, что подтверждено этапом 6. Отдельная квота ограничивает поток HTTP, включая malformed JSON. Дефолты: login 60/мин, register 12/10 мин, refresh 60/мин по IP, теги 30/мин суммарно, фото 10/мин и сообщения 120/мин по пользователю. `429` возвращается с `ErrorResponse` и известным `Retry-After`. Фоновая очистка проверяет записи через 30 секунд, освобождает пустые записи без активных ссылок и закрывает их semaphore. При лимите таблицы новые IP отклоняются до освобождения места. Дефолтный максимум 10 000 IP. На хосте с максимумом 1 и окном 3 секунды второй IP получил 429, после прохода очистки смог войти. В отдельном прогоне с окном 5 секунд девять одновременных запросов дали пять 401 и четыре 429. Верный пароль заблокирован до окончания окна и принят после него. Подмена XFF у клиента без доверенной конфигурации лимит не обошла. Для всех шести общих квот проверили 429 и восстановление с временным трёхсекундным окном Development.

HTTP ошибки маппятся в 400/401/403/404/409/413/415/429/500. При внутренней ошибке ответы не раскрывают SQL или stack trace. Отмена клиента обрабатывается отдельно. Ранний Host filtering отклоняет пакет с неподдерживаемым Host до API, поэтому этот transport-ответ может иметь пустое тело. Журналирование запросов и transport ограничено так, чтобы не сохранять URL с `access_token` и connection token. В локальных журналах не найдено совпадений с 45 проверенными значениями токенов, cookies, credentials, signed URLs и приватных данных. Маркеров `access_token=` нет. `.env` игнорируется и отсутствует в индексе Git, `.env.example` состоит из параметров и placeholders.

### F-01. Временной канал при неверных credentials

- **Серьёзность:** средняя. В локальной среде потенциально позволяет отличить существующие аккаунты по времени ответа. Переносимость результата на внешний deployment не измерялась.
- **Место:** [AuthService.cs, строка 79](../src/ITMO.Buddy/Application/ITMO.Buddy.Application/Auth/AuthService.cs#L79). Ветка `user is null || !passwords.Verify(...)` пропускает BCrypt для неизвестного email.
- **Воспроизведение:** чередовали неверный пароль одноразового зарегистрированного аккаунта и неизвестный локальный email до достижения rate limit. Тела `401 auth.invalid_credentials` совпали. Замеры в пределах одной ручной сессии составили 220–234 мс для существующего аккаунта и 24–25 мс для неизвестного.
- **Минимальное исправление:** в Application выполнять эквивалентную BCrypt-проверку по заранее подготовленному фиктивному хешу с тем же BCrypt cost для отсутствующего пользователя, сохраняя общий внешний ответ и существующий лимит. Подтвердить одинаковый путь и повторить ручную проверку. Ранее принятый слой оставлен без изменений до отдельного согласования.

## 11. OpenAPI & Health

Фактический `/openapi/v1.json` в Development прошёл статическую валидацию OpenAPI 3.1.1. Сверены 27 путей и 35 операций, четыре anonymous auth-операции, Bearer для остальных, схемы DTO и ErrorResponse, повторяющиеся query-ключи и обязательное бинарное поле `file`. В Production документ не опубликован.

Оба health endpoint доступны без JWT. Liveness не вызывает PostgreSQL и MinIO. Readiness проверяет `CanConnectAsync` для PostgreSQL с бюджетом три секунды и возвращает нейтральный статус. В финальном прогоне оба probes вернули 200. После остановки одноразовой БД liveness сохранил 200, readiness вернул 503. Login при недоступной БД вернул нейтральный 500. Readiness не подтверждает доступность MinIO. Health-запросы не запускают миграции.

## 12. File Storage

Подняты временный MinIO из зафиксированного официального исходного кода и Caddy. Для HTTPS применён сертификат с SAN `storage.itmo-buddy.test`, клиент разрешил имя локально и проверил сертификат. Bucket создан перед запуском сценариев. PNG и JPEG дали HTTP `200` и подписанный HTTPS URL. Оба файла прочитаны до истечения ссылки и побайтно совпали с отправленными. Невалидное изображение вернуло `400`, файл размером 5 MiB + 1 байт — `413`. Общий 413 для тела multipart и chunked подтверждён на этапе 6.

`FileCleanup.Enabled=false` отключает фоновый обработчик очереди и reconciliation. Существующий best-effort fast path после замены фото и удаления аккаунта остаётся активен. Он берёт очередь в транзакции, проверяет остаточные ссылки и удаляет объект. При сбое до commit claim откатывается. В одноразовой среде замена PNG на JPEG и удаление аккаунта завершились с пустой очередью. Обе ранее действительные ссылки после удаления соответствующих объектов вернули 404. Пайплайн reconciliation использует возраст объекта, курсор и пакетную проверку отсутствия ссылок. Runtime с включённым worker отдельно не запускался.

## 13. Performance & Scalability

- Поиск, объявления, списки и история выполняют серверную пагинацию. Привязка DTO и контроллеры не фильтруют страницы в памяти. Индексы покрывают ключи связей, историю сообщений и полнотекстовые поля. Глубокий OFFSET и отдельный `COUNT` увеличивают работу БД. Планы на production-распределении данных не измерены.
- Рекомендации выбирают до 200 заголовков с UUID-курсором, затем пакетно загружают признаки. Повторный обход всех кандидатов нужен для глобального ранжирования и `TotalCount`. Priority queue удерживает до окна запрошенной страницы. Поздние страницы и рост пула кандидатов увеличивают объём вычислений и память. Один фактический переход между пачками проверен.
- Профили и авторы для ленты загружаются пакетами. История читает ограниченную страницу и обновляет read receipts в транзакции. Узкие места — возможная конкуренция за advisory locks пары и пользователя, глубокое чтение истории, а также последовательная подготовка карточек и signed URLs при большом числе фото. Дополнительных SQL-циклов в API не обнаружено.
- Upload фото и удаления через FileCleanup могут держать транзакционную блокировку во время I/O к MinIO. Медленное хранилище увеличивает срок удержания блокировки. SignalR-доставка выполняется после commit и добавляет работу к ответу, а сохранённое состояние остаётся доступно независимо от подключений.
- Login limits и skips локальны процессу и сбрасываются при рестарте. SignalR group/user routing ограничен экземпляром без backplane. Для нескольких реплик требуется отдельная политика rate limiting и согласованная маршрутизация SignalR. Нагрузочные тесты, p95/p99 и RPS не измерялись.

## 14. Verification Results

| Статус | Результат |
|---|---|
| Implementation Complete | Да, 35 REST-операций, SignalR и эксплуатационные endpoints |
| Build Verified | Да, `clean → restore → build --no-restore`, семь проектов, 0 ошибок и предупреждений |
| Static Audit Passed | Архитектура и контракты прошли проверку. Открыто замечание безопасности F-01 |
| DI Verified | Да, все 11 сервисов фактически разрешены в HTTP request scopes |
| Runtime Verified | Да, локальные Development и Production запущены |
| HTTP Smoke Verified | Да, все 35 операций пройдены вручную на изолированных данных |
| SignalR Smoke Verified | Да, реальные WebSocket/SSE, пять событий, адресация, expiry и reconnect |
| PostgreSQL Verified | Да, обе миграции, snapshot, индексные метаданные и сквозные сценарии |
| MinIO Verified | Да, PNG/JPEG и HTTPS чтение обоих signed URLs |
| Security Smoke Verified | Подтверждены JWT, CSRF, лимиты и ошибки. Временной канал F-01 воспроизведён |
| Production Ready | Не подтверждено |

Автоматизированные Unit-, Integration- и E2E-тесты не создавались и не запускались. Валидатор OpenAPI использован для проверки документа, HTTP и SignalR — для ручных сценариев. Временные данные и инфраструктура созданы отдельно от рабочей базы.

## 15. Not Verified

- Полное истечение десятиминутного окна в реальном времени. В финальном Development использовалось окно 5 секунд, а на этапе 6 Production показал `Retry-After` около 600 секунд.
- WebSocket поверх HTTP/2, преднамеренный отказ доставки SignalR после commit и recovery/backplane на нескольких репликах. Offline-сценарий подтверждён.
- Фоновый FileCleanup при `Enabled=true`, работа reconciliation и повторные удаления при сбоях MinIO. Fast path при штатном удалении проверен.
- Production traffic, планы SQL на репрезентативной deployed БД, нагрузочные показатели, инфраструктурный мониторинг и журналы внешнего deployment reverse proxy. Требования к исключению токенов и приватных данных из логов задокументированы в runbook.

## 16. Known Limitations

Rate Limiting, пропуски рекомендаций и адресация SignalR локальны экземпляру. Logout отзывает refresh, срок действующего JWT сохраняется. Readiness подтверждает PostgreSQL. Фоновая очистка выключена по умолчанию, а успешная смена фото и удаление аккаунта могут удалить прежний объект через fast path. Файл [локального запуска](IB-006-local-run.md) содержит конфигурацию TLS, PostgreSQL, MinIO, переменных окружения, миграций, CORS, probes и proxy logs. Образец `.env.example` требует подставить собственные параметры.

## 17. Changed Files

Этап 7 добавляет только `docs/IB-006-final-audit.md`. Этапы 1–6 добавили или изменили API Composition Root, 12 контроллеров, адаптеры Identity, Auth cookie, Origin и SignalR, конфигурации HTTP и OpenAPI, health check, сервисы ограничений, настройки API и профиль запуска. Также добавлены `.env.example` и `docs/IB-006-local-run.md`, обновлён README. Domain, Application, обоим Infrastructure-проектам, DTO, модели и миграциям коммиты IB-006 изменений не внесли.

## 18. Git State

Ветка `feature/IB-006`. Перед началом `git fetch origin` подтвердил совпадение локального и удалённого `621bcd57a2b853cccc11b356d844c0612efedcca`. База включает коммиты этапов 1–6 `e28e295`, `6f477b9`, `e21fb3c`, `2327eca`, `55b64c6`, `621bcd5`. SHA коммита документа определяется командой `git log -1 --format=%H -- docs/IB-006-final-audit.md`. Совпадение удалённого SHA и чистота дерева проверяются после публикации и отражаются в микроотчёте. PR и merge в объём этапа не входят.

## 19. Final Recommendation

**ACCEPT WITH LIMITATIONS** для реализации Backend MVP, локально проверенной на изолированной инфраструктуре. Для открытого production-развёртывания требуется согласованное устранение F-01 и проверка журналов, мониторинга и поведения реальной схемы развёртывания. Финальное решение о приёмке остаётся за ревьюером.
