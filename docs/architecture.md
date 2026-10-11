# Architecture

> مستند بر اساس کد واقعی در تاریخ 2026-09-27. هر چیز پیاده‌نشده صریحاً «Not implemented» شده است.

## Overview

پروژه ASP.NET Core 9 با Clean Architecture در ۵ لایه. UI (Blazor Server) و REST API در **یک پروسه** اجرا می‌شوند (`Web/Program.cs` کنترلرهای اسمبلی Api را با `AddApplicationPart` ثبت می‌کند) تا کل MVP با یک `dotnet run` بالا بیاید. پروژه `Api` یک `Program.cs` مستقل هم دارد (Swagger + CORS برای 5173) برای اجرای جداگانه API.

## Technology Stack

- .NET 9 / ASP.NET Core (Razor Components, MVC Controllers)
- EF Core 9 + Microsoft.Data.Sqlite
- System.Text.Json (payload بخش‌ها + DTO ها با JsonStringEnumConverter)
- xUnit + Microsoft.Data.Sqlite in-memory برای تست
- بدون ORM دیگر، بدون کتابخانه UI، بدون DI container ثالث

## Frontend Architecture

- **Framework:** Blazor Server — `InteractiveServerRenderMode(prerender: false)` در `src/AI.TeachingAssistant.Web/Components/App.razor`
- **Routing:** `Components/Routes.razor` (Router + `FocusOnNavigate Selector="h1"`)؛ مسیرها: `/`, `/lessons/new`, `/lessons`, `/lessons/{id:int}`, `/profile`
- **Pages:** `Index.razor` (Home), `NewLesson.razor` (ویزار ۲ مرحله‌ای), `Lessons.razor`, `LessonDetail.razor`, `Profile.razor`
- **Shared:** `TeachingPackageView.razor` (تب‌ها/progress/کپی/ویرایش/regenerate + polling)، `PromptCard.razor`، ویوهای ۴ بخش (`LessonPlanView`, `EducationalContentView`, `ActivityView`, `QuizView`)
- **State Management:** state محلی کامپوننت‌ها (فیلدهای private) — هیچ state manager سراسری وجود ندارد
- **API calls:** `Services/MadreseAIApiClient.cs` — typed client روی `HttpClient` (BaseAddress = BaseUri همان هاست)؛ خطاها را به `MadreseAIApiException` با پیام فارسی سرور تبدیل می‌کند
- **Polling:** `TeachingPackageView` با `System.Timers.Timer` (1500ms) وضعیت را از `GET /api/lessons/{id}` می‌خواند؛ شروع polling در `OnAfterRenderAsync` رندرِ حامل داده (نه لزوماً firstRender — رفع باگ 2026-09-27)
- **Styling:** CSS سفارشی `wwwroot/app.css` (سیستم طراحی: `docs/design-system.md`)؛ فونت Vazirmatn از CDN jsDelivr
- **RTL:** `<html lang="fa" dir="rtl">` در `App.razor` + استفاده از propertyهای منطقی (`margin-inline`, `padding-inline-start`) در CSS
- **JS interop:** فقط `window.copyToClipboard` (تعریف inline در `App.razor` با fallback برای non-secure context) و `confirm` پیش‌فرض مرورگر

## Backend Architecture

- **Controllers:** `Api/Controllers/LessonsController.cs` (CRUD + generate + save section)، `Api/Controllers/TeacherController.cs` — نازک؛ فقط orchestration
- **Services (Application):**
  - `LessonService` — CRUD درس + `ValidateLesson` (پیام‌های فارسی) + `NormalizeSections`
  - `TeacherService` — پروفایل معلم پیش‌فرض
  - `SectionGenerationService` — الگوی عمومی prompt → AI → parse → validate (۲ تلاش) → map به دامنه، برای هر ۶ بخش
  - `GenerationCoordinator` — تولید یک بخش یا کل بسته؛ وضعیت Lesson/Package و `CompletedSections` را مدیریت می‌کند
  - `PackageSectionService` — ذخیره ویرایش معلم با اعتبارسنجی مجدد
- **Middleware:** `ErrorHandlingMiddleware` — ValidationException→400 فارسی، هر چیز دیگر→500 «خطای غیرمنتظره‌ای پیش آمد»
- **Validation:** دو لایه — `LessonService.ValidateLesson` (درخواست‌ها) و `AiResponseValidator` (خروجی AI: تعداد سوالات، کلید درون گزینه‌ها، پیوستگی زمان‌بندی طرح درس با `LessonPlanModel.HasRealisticSchedule`، ...)
- **DTOs:** همه در `Application/Dtos/Dtos.cs` (`CreateLessonRequest`, `UpdateLessonRequest`, `LessonSummaryDto`, `TeachingPackageDto`, `QuestionDto`, ...) — entity هرگز از API خارج نمی‌شود (`DtoMapper`)
- **Rate limiting:** پالیسی `ai` (20/min) روی endpointهای generate، `general` (200/min) روی کل کنترلرها — فقط در پروژه Api standalone تعریف شده؛ در میزبانی یکپارچه از Web این rate limiter ثبت نمی‌شود (Technical Debt)

## Data Access

- **ORM:** EF Core 9 (Code-first)
- **DbContext:** `Infrastructure/Persistence/AppDbContext.cs` — maxLength ها و cascade ها در `OnModelCreating`
- **Repositories:** `TeacherRepository`, `LessonRepository`, `TeachingPackageRepository` در `Infrastructure/Repositories/Repositories.cs` (اینترفیس‌ها در `Application/Interfaces/IRepositories.cs`)
- **Payload pattern:** محتوای تولیدی هر بخش به‌صورت JSON text در ستون `PayloadJson` ذخیره و با `PayloadJson.Serialize/Deserialize` (camelCase + enum-as-string) به Model دامنه تبدیل می‌شود

## Database

- **Provider:** SQLite (connection string `Data Source=madreseai.db`) — PostgreSQL نه
- **Entities (Domain/Entities):** `Teacher`, `Lesson` (شامل `PackageStatus`, `RequestedSections`, `LastError`), `TeachingPackage` (`CompletedSections`), `LessonPlan`, `EducationalContent`, `ClassroomActivity`, `Quiz`, `MediaPrompt` (Kind: Image/Video)
- **Value/Model:** `GenerationProgress`، مدل‌های payload در `Domain/Models/` (`LessonPlanModel`, `EducationalContentModel`, `ActivityModel`, `QuizModel`/`QuestionModel`, `ImagePromptModel`, `VideoPromptModel`)
- **Relationships:** Teacher 1→* Lesson (cascade)؛ Lesson 1↔1 TeachingPackage (cascade)؛ Package 1↔1 هر بخش (cascade)؛ Package 1→* MediaPrompts (cascade). ایندکس‌ها: `IX_Lessons_TeacherId_CreatedAt` و unique index روی FK هر بخش
- **Migrations:** دو migration — `Infrastructure/Migrations/20260926084831_InitialCreate.cs` و `20261008000000_SmartClassBriefFields.cs`
- **Seed:** `DatabaseInitializer.InitializeAsync` — در استارتاپ: migrate + درج معلم دمو «مریم احمدی» و دو درس نمونه («سلول چیست؟» draft با ۶ بخش، «زنجیره غذایی» draft با ۳ بخش) فقط اگر جدول خالی باشد
- فایل db در `.gitignore` است

## API Structure

| Method | Route | رفتار |
| ------ | ----- | ----- |
| POST | `/api/lessons` | ایجاد درس (validation فارسی) → 201 + `LessonSummaryDto` |
| GET | `/api/lessons` | لیست درس‌های معلم پیش‌فرض (جدید→قدیم) |
| GET | `/api/lessons/{id}` | `TeachingPackageDto` کامل (شامل `progress` و `requestedSections`) |
| PUT | `/api/lessons/{id}` | ویرایش مشخصات درس |
| DELETE | `/api/lessons/{id}` | حذف آبشاری درس+بسته → 204 |
| POST | `/api/lessons/{id}/generate` | صف تولید بخش‌های `RequestedSections` → 202 |
| POST | `/api/lessons/{id}/generate/{section}` | صف تولید مجدد فقط آن بخش (`lesson-plan`, `content`, `activity`, `quiz`, `image-prompt`, `video-prompt`) → 202 |
| PUT | `/api/lessons/{id}/sections/{section}` | ذخیره ویرایش JSON بخش‌های ۱–۴ → `TeachingPackageDto` |
| GET/PUT | `/api/teacher` | پروفایل معلم پیش‌فرض |

Swagger: در اجرای standalone Api در Development فعال است (`Api/Program.cs`). Not exposed from the integrated Web host.

## Content Generation Architecture

```text
UI «✨ ساخت محتوای کلاس» (NewLesson.razor)
   → POST /api/lessons           (LessonService.CreateAsync — validation + RequestedSections)
   → POST /api/lessons/{id}/generate
        └─ GenerationJobQueue.Enqueue(id, ContentTypes.All)   [Channel درون‌حافظه]
   → GenerationJobWorker (BackgroundService، gate تک‌همزمانی)
        ├─ GeneratePackageAsync: به‌ترتیب برای هر بخشِ درخواستی GenerateSectionAsync
        └─ GenerateSectionAsync(lessonId, sectionType)
             ├─ SectionGenerationService.Generate{X}Async
             │    ├─ {X}PromptBuilder.BuildUserPrompt(GenerationContext)
             │    ├─ IAIProvider.CompleteAsync
             │    ├─ AiJson.Parse (استخراج JSON از داخل code fence)
             │    └─ AiResponseValidator (۲ تلاش)
             ├─ ذخیره PayloadJson در entity بخش + افزودن کد بخش به CompletedSections
             └─ به‌روزرسانی PackageStatus (Draft/Generating → Ready؛ خطا → Failed + LastError فارسی)
```

- UI با polling همان progress را می‌خواند؛ هنگام `ready`، callback `OnCompleted` هدر صفحه را به حالت 🎉 می‌برد
- Partial failure: خطای یک بخش، بخش‌های قبلی را نگه می‌دارد؛ در پایان اگر هر بخشی fail شده باشد PackageStatus=Failed و پیام اولین خطا نمایش داده می‌شود؛ بخش گم‌شده از تب خودش قابل تولید مجدد است

## AI Provider Architecture

```text
SectionGenerationService (Application)
          ↓
      IAIProvider                     Application/Common/IAIProvider.cs
      (AiRequest{SystemPrompt,UserPrompt,MaxTokens=2000,Temperature=per-section}
       → AiResponse{Content})
          ↓
MockAIProvider                          Infrastructure/Ai/MockAIProvider.cs
  - پیش‌فرض؛ AddSingleton
  - MockContentFactory: context را از user prompt پارس می‌کند
    (موضوع/درس/پایه/مدت/سطح/سبک) و خروجی فارسی topic-aware می‌سازد
  - شمارنده call بین دو استخر سؤال آزمون می‌چرخد تا «تولید مجدد» خروجی متفاوت بدهد
  - هر خروجی باید از AiResponseValidator عبور کند (تست‌شده)

OpenAiCompatibleProvider                Infrastructure/Ai/OpenAiCompatibleProvider.cs
  - Endpoint: {BaseUrl}/chat/completions با Bearer ApiKey
  - **xKiro (وصل‌شده):** BaseUrl = https://api.xkiro.com/v1
  - فعال‌سازی: AI_PROVIDER=xkiro (یا openai) — پیش‌فرض امن mock است
  - ApiKey فقط از env/secret: AI_API_KEY یا XKIRO_API_KEY — هرگز در repo/log/exception نیست
  - خطاهای فارسی استاندارد: کلید تنظیم نشده / احراز هویت ناموفق / rate limit / timeout
  - Model پیش‌فرض: qwen/qwen3.5-flash:free (قابل override با AI_MODEL؛ فرمت vendor/model از کاتالوگ GET /v1/models)
```

وضعیت اتصال (بروزرسانی Step 3):

```text
Real AI Provider: Implemented — xKiro (OpenAI-compatible)
Base URL:         https://api.xkiro.com/v1
Provider:         xKiro
Authentication:   API Key via environment variable (AI_API_KEY یا XKIRO_API_KEY)
Model:            From configuration (AI_MODEL؛ پیش‌فرض qwen/qwen3.5-flash:free، فرمت vendor/model)
Mock Provider:    Available (پیش‌فرض توسعه)
RAG:              Not implemented
```

- **Prompt Builders** (`Application/Prompts/`): `PromptContextBuilder` (پرامپت لایه‌ای مشترک: نقش دستیار + پروفایل معلم + مشخصات درس + hint سطح + سبک تدریس + زمینه مشترک بخش‌های قبلی + قواعد کیفیت سراسری فارسی/ضد-hallucination) و شش builder جدا: `LessonPlanPromptBuilder`, `EducationalContentPromptBuilder`, `ActivityPromptBuilder`, `QuizPromptBuilder`, `ImagePromptBuilder`, `VideoPromptBuilder` — هر builder یک `Temperature` اختصاصی دارد (0.3 آزمون، 0.7 طرح/محتوا، 0.8 فعالیت/پرامپت‌ها) و زمینه مشترک بخش‌ها از `SharedLessonContext` می‌آید (`GenerationContext.Shared`؛ در `GenerationCoordinator.BuildSharedContext` از طرح درس/محتوای موجود بسته ساخته می‌شود). `SectionGenerationService` علاوه بر اعتبارسنجی ساختار، کنترل خلوص زبان فارسی انجام می‌دهد (نویسه‌های چینی/روسی/... یا واژه لاتین بیرون از الگوی «فارسی (English)»؛ اصطلاحات استاندارد اندازه کاغذ مثل «برگه A4» مجاز است تا false-positive و retry بی‌مورد رخ ندهد — رد شدن یک snippet کوتاه تشخیصی در لاگ می‌نویسد) و خطاهای گذرای Provider (timeout/شبکه/429/5xx) را یک‌بار دوباره امتحان می‌کند. **اندازه‌گیری توکن (فاز ۲):** `OpenAiCompatibleProvider` مقدار `usage` پاسخ گیت‌وی (prompt/completion/total tokens) را در `AiResponse.Usage` برمی‌گرداند و لاگ می‌کند؛ `SectionGenerationService` جمع واقعی توکن هر بخش را با تعداد تلاش‌ها لاگ می‌کند (`Token usage for …`). **مدیریت مدل و failover کنترل‌شده (فاز ۳):** مدل اصلی `qwen/qwen3.5-flash:free` می‌ماند و مدل جایگزین اختیاری `FallbackModel` دارد (پیش‌فرض خالی = failover غیرفعال؛ مقدار فعلی `qwen/qwen3.7-flash:free` — وجود در کاتالوگ ۱۴۱ مدلی و کارکرد واقعی‌اش با فراخوانی xKiro تأیید شده). در `OpenAiCompatibleProvider` فقط برای خطاهای گذرای دسترس‌پذیری (timeout/شبکه/429/5xx — همان طبقه‌بندی `IsTransient`) یک تلاش واحد با همان قرارداد کامل درخواست (همان پرامپت/دما/MaxTokens؛ تنها مدل فرق می‌کند) روی مدل جایگزین انجام می‌شود؛ خطاهای قطعی (کلید ناموجود/احراز هویت/پارس/محتوای خالی) هرگز failover نمی‌شوند، حلقه یا failover بازگشتی وجود ندارد و در مسیر عادی (موفقیت مدل اصلی) هیچ درخواست اضافه‌ای زده نمی‌شود — رجیستری لاگ: `AI failover: … failed (transient); retrying once via fallback model …`.
- **Regeneration (`تولید مجدد`):** رجنیریت یک بخش، هشی فشرده از خروجی قبلی را فقط به‌عنوان «مرجع تفاوت» به پرامپت اضافه می‌کند (`RegenerationInfo` در `GenerationContext`؛ بلاک مشترک در `PromptContextBuilder.AppendRegenerationBlock`؛ ساخت در `GenerationCoordinator.BuildRegeneration`) — مدل صریحاً از کپی/پارافریز نسخه‌ی قبلی منع می‌شود و باید رویکرد آموزشی متفاوتی بدهد. کنترل تکراری سبک (Jaccard ≥0.65 + بازاستفاده ≥60٪ خطوط، بدون embedding) در `RegenerationSupport.IsNearDuplicate` با حداکثر یک retry با «توجه ویژه» تنوع، در `SectionGenerationService`. Controller/worker وضعیت درس را قبل از ۲۰۲/اجرا به Generating می‌برند تا polling هرگز رجنیریت را بی‌اثر نبیند؛ در شکست، وضعیت قبلی (Ready برای بسته‌ی کامل، وگرنه Failed) برمی‌گردد و محتوای قبلی دست‌نخورده می‌ماند.
- **AI-related configuration:** بخش `"AI"` در `Api/appsettings.json` (Provider/ApiKey/BaseUrl/Model/FallbackModel/TimeoutSeconds/MaxTokens)؛ متغیرهای محیطی `AI_PROVIDER`, `AI_API_KEY`, `AI_BASE_URL`, `AI_MODEL`, `AI_FALLBACK_MODEL` اولویت دارند (`Infrastructure/DependencyInjection.cs`)

## Dependency Injection

`Infrastructure/DependencyInjection.AddTeachingAssistantInfrastructure`:
- `AddDbContext<AppDbContext>` (SQLite)
- `IAIProvider` → Mock (singleton) یا OpenAI-compatible (`AddHttpClient`) بر اساس config
- Repository ها (scoped)، سرویس‌های Application (scoped)
- `GenerationJobQueue` (singleton) + `GenerationJobWorker` (HostedService)

در `Web/Program.cs`: ثبت کنترلرهای Api با `AddApplicationPart`، `ApiBehaviorOptions` برای خطاهای model-state فارسی، و `MadreseAIApiClient` scoped. در `Api/Program.cs` (standalone): RateLimiter + CORS(5173) + Swagger(dev) + middleware خطا.

## Data Flow (نمونه: تولید کامل)

```text
فرم معلم → CreateLessonRequest → LessonService (validate) → Lessons جدول (Draft, RequestedSections)
        → POST generate → GenerationJobQueue → Worker
        → برای هر بخش: PromptBuilder → MockAIProvider → AiJson → AiResponseValidator
        → LessonPlans/EducationalContents/ClassroomActivities/Quizzes/MediaPrompts (PayloadJson)
        → CompletedSections += code → PackageStatus = Ready
UI: GET /api/lessons/{id} هر ۱.۵s → progress → رندر تب‌ها از همان DTO
ویرایش: PUT sections/{section} → PackageSectionService (validate) → PayloadJson جدید
```

## Current Architecture Diagram

```text
┌─────────────────────────── یک پروسه (Web/Program.cs) ───────────────────────────┐
│                                                                                  │
│  Blazor Server UI (Web/Components)          REST API (Api/Controllers)          │
│   Index / NewLesson / Lessons /              LessonsController / TeacherController│
│   LessonDetail / Profile                            │        ↑                   │
│        │  MadreseAIApiClient (HTTP loopback)        │        │ rate limit "ai"   │
│        └───────────────────────────────────────────►│        │                   │
│                                                     ▼        │                   │
│                              GenerationJobQueue (Channel, in-memory)              │
│                                                     │                            │
│                              GenerationJobWorker (BackgroundService)             │
│                                                     ▼                            │
│                    GenerationCoordinator → SectionGenerationService              │
│                                            │              │                      │
│                                            │              ▼                      │
│                                            │         IAIProvider                 │
│                                            │      ┌────┴─────────┐               │
│                                            │   MockAIProvider   OpenAiCompatible │
│                                            │   (پیش‌فرض)        (بدون کلید)      │
│                                            ▼                                     │
│                  Repositories → EF Core AppDbContext → SQLite (madreseai.db)     │
└──────────────────────────────────────────────────────────────────────────────────┘
```

## Architecture Decisions (مشاهده‌پذیر در کد)

- **Single-process hosting:** Web کنترلرهای Api را با `AddApplicationPart` میزبانی می‌کند تا MVP با یک دستور اجرا شود.
- **SQLite** برای MVP persistence؛ سوییچ به Provider دیگر فقط با تغییر `UseSqlite`.
- **AI Abstraction:** تولید پشت `IAIProvider` است؛ Mock/OpenAI-compatible قابل تعویض با config.
- **JSON-payload sections:** هر بخش به‌صورت `PayloadJson` ذخیره می‌شود تا تغییر schema بخش‌ها migration لازم نداشته باشد.
- **Background queue:** تولید از HTTP جدا شده تا درخواست‌ها فوری 202 برگردند و UI با polling پیش برود.
- **Persian-first errors:** هم model-state و هم middleware، پیام فارسی برمی‌گردانند؛ استک هرگز لو نمی‌رود.
- **Authentication:** Not implemented — معلم پیش‌فرض seed می‌شود.

## Known Technical Debt

| # | Problem | Location | Impact | Suggested future direction |
| - | ------- | -------- | ------ | -------------------------- |
| 1 | Rate limiter و CORS فقط در `Api/Program.cs` تعریف شده‌اند؛ در میزبانی یکپارچه (Web) endpointهای generate بدون rate limit هستند | `src/AI.TeachingAssistant.Api/Program.cs` vs `src/AI.TeachingAssistant.Web/Program.cs` | سوءاستفاده/هزینه در اتصال AI واقعی | انتقال AddRateLimiter/CORS به `DependencyInjection` مشترک Infrastructure |
| 2 | صف تولید in-memory و بدون durability؛ crash بین Enqueue و اجرا → درس در حالت generating قفل می‌ماند (بدون timeout/recovery) | `Infrastructure/Generation/GenerationJob.cs` | نیاز به intervention دستی DB در crash | پیاده‌سازی reclaim بر اساس UpdatedAt یا صف پایدار |
| 3 | `DtoMapper.ToPackageDto` به `GenerationCoordinator.ParseSections` (لایه Services) وابسته است — coupling Mapping→Services | `Application/Mapping/DtoMapper.cs` | تست و استفاده مجدد سخت‌تر | انتقال ParseSections به Domain/util مشترک |
| 4 | header صفحه درس برای چیپ‌های خلاصه، جدا از package یک `GET /api/lessons` کامل می‌زند (فیلدهای Subject/Grade روی DTO بسته نیستند) | `Web/Components/Pages/LessonDetail.razor` | یک call اضافه per load | افزودن فیلدهای درس به `TeachingPackageDto` |
| 5 | `Web/Program.cs` با `new HttpClient` مستقیم MadreseAIApiClient می‌سازد (نه IHttpClientFactory) | `src/AI.TeachingAssistant.Web/Program.cs` | socket churn جزئی؛ تست‌پذیری کمتر | `AddHttpClient<MadreseAIApiClient>` |
| 6 | ویرایش فقط برای بخش‌های ۱–۴؛ پرامپت تصویر/ویدئو فقط کپی | `PackageSectionService.cs` | محدودیت UX | تعمیم ValidateAndStore به MediaPrompt models |
| 7 | دو Program.cs (Api و Web) تنظیمات موازی دارند (seed/migrate هر دو اجرا می‌کنند) — خطر واگرایی config | `Api/Program.cs`, `Web/Program.cs` | نگهداری دوگانه | استخراج host builder مشترک یا حذف host مستقل Api در MVP |
| 8 | هیچ integration test برای HTTP pipeline (کنترلر/middleware) وجود ندارد؛ تست‌ها سرویس‌محورند | `src/AI.TeachingAssistant.Tests/` | ریسک رگرسیون در contract | WebApplicationTest با SQLite in-memory |

## Future Architecture Direction

- Real AI Provider: **Implemented — xKiro** (از طریق OpenAiCompatibleProvider)
- RAG / Teacher Knowledge Base: **Not implemented** (embeddings، vector DB، آپلود PDF — مرحله بعد)
- صف پایدار/recovery؛ PostgreSQL؛ Authentication و Teacher-scoped data؛ Responses API به‌جای chat/completions (اختیاری)؛ structured output سخت‌گیرانه‌تر (response_format json_schema)
