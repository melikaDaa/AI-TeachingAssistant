# AGENTS.md

قوانین و زمینه کاری برای هر Agent‌ای که روی این پروژه کار می‌کند. این فایل فقط چیزهایی را ثبت می‌کند که از کد فعلی قابل اثبات هستند.

## Project

**مدرس‌یار (MadreseAI)** — دستیار هوشمند معلم

## Project Status

MVP — فعال و قابل دمو. جریان کامل «ساخت درس → تولید محتوا → مشاهده/ویرایش/کپی/تولید مجدد → ذخیره → تاریخچه» کار می‌کند.

## Product Goal

معلم ایرانی موضوع کلاس خود را می‌دهد و مدرس‌یار بسته آموزشی کامل (طرح درس، محتوای آموزشی، فعالیت کلاسی، آزمون، پرامپت تصویر و ویدئو) را به زبان فارسی و متناسب با پایه/سطح دانش‌آموزان آماده می‌کند. جزئیات در `docs/product.md`.

## Current Technology Stack

- **.NET 9** — ASP.NET Core Clean Architecture با ۵ پروژه:
  - `src/AI.TeachingAssistant.Domain` — Entityها و Modelهای دامنه
  - `src/AI.TeachingAssistant.Application` — DTOs، سرویس‌ها، Prompt Builderها، اعتبارسنجی خروجی AI
  - `src/AI.TeachingAssistant.Infrastructure` — EF Core/SQLite، Repositoryها، AI Providerها، صف تولید
  - `src/AI.TeachingAssistant.Api` — کنترلرهای REST، middleware خطا، rate limiting
  - `src/AI.TeachingAssistant.Web` — Blazor Server ( SSR + Interactive Server )
- **Blazor Server** برای UI (فایل `App.razor` حالت `InteractiveServerRenderMode(prerender: false)` را ثبت می‌کند)
- **EF Core 9 + SQLite** (`madreseai.db`)
- **xUnit** برای تست (`src/AI.TeachingAssistant.Tests` — ۳۰ تست)
- فونت **Vazirmatn** از CDN؛ **بدون** کتابخانه UI ثالث (CSS سفارشی در `src/AI.TeachingAssistant.Web/wwwroot/app.css`)

## Current Architecture

```text
Blazor Server UI (Web)
   ↓  MadreseAIApiClient (HTTP به همان هاست)
API Controllers (Api)  ← در همان پروسه Web هم سرو می‌شوند (Web/Program.cs)
   ↓
Application Services (LessonService, GenerationCoordinator, ...)
   ↓
Repositories (Infrastructure) → EF Core → SQLite
```

تولید محتوا از HTTP جدا است: کنترلر فقط job را در `GenerationJobQueue` صف می‌کند و `GenerationJobWorker` (BackgroundService) یکی‌یکی تولید می‌کند. جزئیات کامل در `docs/architecture.md`.

## Current AI Architecture

```text
SectionGenerationService (Application)
          ↓
      IAIProvider  (Application/Common/IAIProvider.cs)
          ↓
MockAIProvider  (پیش‌فرض توسعه، آفلاین، topic-aware)
OpenAiCompatibleProvider  →  xKiro (https://api.xkiro.com/v1)
```

- **AI Provider: xKiro** از طریق OpenAI-compatible API (`/chat/completions`)
- **Mock Provider: Available** — پیش‌فرض (`AI_PROVIDER=mock` یا نامشخص)
- فعال‌سازی xKiro: `AI_PROVIDER=xkiro` + `AI_API_KEY` (یا `XKIRO_API_KEY`)
- **API Key: فقط Environment Variable / Secret Manager** — هرگز در repo، log یا exception
- Model از `AI_MODEL` (پیش‌فرض `qwen/qwen3.5-flash:free`؛ فرمت vendor/model از کاتالوگ xKiro)؛ Base URL از `AI_BASE_URL` (پیش‌فرض xKiro)
- مدل جایگزین از `AI_FALLBACK_MODEL` / کلید `AI:FallbackModel` (پیش‌فرض خالی = failover غیرفعال)؛ در فاز ۳ فقط برای خطاهای گذرای دسترس‌پذیری (timeout/شبکه/429/5xx) یک تلاش واحد با همان قرارداد درخواست روی مدل جایگزین انجام می‌شود — `qwen/qwen3.5-plus:free` در کاتالوگ و اجرای واقعی xKiro تأیید شده است
- Prompt Builder جدا برای هر بخش در `src/AI.TeachingAssistant.Application/Prompts/`
- خروجی AI با `AiResponseValidator` اعتبارسنجی می‌شود (۱ retry؛ invalid JSON → تلاش دوم → در صورت شکست Failed)
- هر builder پرامپت یک `Temperature` اختصاصی دارد (آزمون 0.3؛ طرح/محتوا 0.7؛ فعالیت/پرامپت‌ها 0.8)؛ بعد از هر بخش، زمینه فشرده‌ای از طرح درس/محتوای همان بسته (`SharedLessonContext`) به پرامپت بخش‌های بعدی تزریق می‌شود؛ کنترل خلوص زبان فارسی (نویسه‌های زبان‌های دیگر / لاتین بیرون از «فارسی (English)»؛ اندازه‌های کاغذ مثل A4 مجازند) و retry خطاهای گذرای Provider نیز در `SectionGenerationService` است؛ مصرف توکن واقعی هر فراخوانی از `usage` گیت‌وی خوانده و لاگ می‌شود (`AiResponse.Usage` + خط `Token usage for …`)
- Regenerate یک بخش، خلاصه‌ی خروجی قبلی را فقط به‌عنوان «مرجع تفاوت» به پرامپت می‌دهد (`RegenerationInfo` + `RegenerationSupport`)، چک تکراریِ سبک دارد (Jaccard ≥0.65 + ≥60٪ خطوط تکراری → یک retry با تأکید تنوع) و وضعیت درس را هنگام رجنیریت به Generating و پس از پایان/شکست به وضعیت درست برمی‌گرداند
- **RAG: Not implemented**

## Database

- **SQLite** از طریق EF Core (`AppDbContext` در `Infrastructure/Persistence/`)
- یک migration: `InitialCreate` (تاریخ 2026-09-26)
- جداول: `Teachers`, `Lessons`, `TeachingPackages`, `LessonPlans`, `EducationalContents`, `ClassroomActivities`, `Quizzes`, `MediaPrompts` — همه حذف آبشاری از Teacher
- Seed: یک معلم دمو («مریم احمدی») و دو درس نمونه در `DatabaseInitializer`
- فایل `.gitignore` شده (`*.db`) — دیتای local کامیت نمی‌شود

## Frontend Rules

- صفحات Razor در `src/AI.TeachingAssistant.Web/Components/Pages/`، کامپوننت‌های اشتراکی در `Components/Shared/`
- **RTL فارسی سراسری** (`<html lang="fa" dir="rtl">` در `App.razor`)
- کامپوننت‌ها با `MadreseAIApiClient` تایپ‌شده با API حرف می‌زنند؛ entity مستقیماً به UI نمی‌رسد
- اتصال text inputها با `@bind-value:event="oninput"` است — این الگو را بازگردان نکن (باگ blur-race قبلاً با آن رفع شده)
- طراحی سیستم فعلی: `docs/design-system.md` — نام کلاس‌های CSS قرارداد صفحات است؛ تغییر نام کلاس یعنی شکستن صفحات

## Backend Rules

- Controller ها نازک هستند؛ منطق در سرویس‌های Application است
- خطاها به‌صورت `ValidationException` (400 با پیام فارسی) یا از `ErrorHandlingMiddleware` (500 عمومی فارسی) — استک‌تریس هرگز به کاربر نمی‌رسد
- تولید محتوا هرگز داخل Controller انجام نمی‌شود؛ همیشه از صف job می‌گذرد
- Endpoint های تولید rate-limited هستند (`"ai"`: 20 درخواست/دقیقه)

## UI/UX Rules

- سبک «Calm AI Education»: پالت آبی (`#2196F3` / `#0D47A1` / `#E3F2FD` / `#90CAF9`)، فضای سفید زیاد، بدون گرادیان سنگین و انیمیشن اضافه
- CTA اصلی فقط `#2196F3`؛ قرمز فقط برای حذف (با تأیید `confirm`)
- هر صفحه «اقدام بعدی واضح» دارد؛ حالت‌های لودینگ/خالی/خطا پیام فارسی دستیارمانند دارند
- جزئیات: `docs/design-system.md`

## Important Constraints

- **Authentication نداریم** — یک معلم پیش‌فرض (seed) برای همه است
- **Provider واقعی: xKiro** — فعال‌سازی فقط با env (`AI_PROVIDER=xkiro`)؛ بدون کلید، سیستم روی Mock می‌ماند
- **Secret فقط از Environment/Secret Manager** — هرگز در appsettings، git، log، exception یا frontend
- **PostgreSQL / Redis / RAG / Vector DB / PDF / Google Flow API وجود ندارند**
- Contract های عددی بخش‌ها (`ContentTypes`: 1=طرح درس ... 6=پرامپت ویدئو) قرارداد پایدار API هستند
- Migration های موجود را خراب نکن؛ تغییر schema فقط با migration جدید
- `RequestedSections` ذخیره‌شده روی Lesson مبنای تولید انتخابی و فیلتر تب‌های UI است

## Things Not Implemented Yet

- Authentication و چند-کاربره
- PostgreSQL، Redis، RAG، آپلود PDF (RAG مرحله بعد)
- ویرایش ساختاریافته پرامپت‌های تصویر/ویدئو (فقط کپی ممکن است)
- صف تولید مقاوم به ری‌استارت (صف فعلی in-memory است)
- Responses API (فعلاً `/chat/completions` کافی است) و structured output با json_schema

## Development Principles

- قبل از تغییر، کد موجود را بخوان؛ ساختار موجود را دوباره نساز
- Current را با Future قاطی نکن؛ چیزهای پیاده‌نشده را پیاده‌شده گزارش نکن
- بعد از هر تغییر: `dotnet build` + `dotnet test` (۳۰ تست باید سبز شود)
- فلو دمو کامل باید همیشه سالم بماند: Home → ساخت محتوای جدید → فرم → انتخاب محتوا → Generate → مشاهده بسته → ویرایش/کپی/تولید مجدد → درس‌های من → بازگشایی

## Audit Snapshot

```text
Audit date: 2026-09-27
Current branch: main (آخرین کامیت: 63d52f4 «مدرس‌یار MVP: دستیار هوشمند معلم با UI بازطراحی‌شده»)
Frontend: Blazor Server (.NET 9)، RTL فارسی، CSS سفارشی، بدون کتابخانه UI
Backend: ASP.NET Core Controllers + Application Services + Background worker
Database: SQLite via EF Core 9 (1 migration: InitialCreate + seed)
AI: IAIProvider → MockAIProvider (پیش‌فرض) / OpenAiCompatibleProvider → xKiro (فعال با AI_PROVIDER=xkiro + کلید از env)
Authentication: Not implemented
RAG: Not implemented
Testing: xUnit — ۳۰ تست (همه سبز؛ شامل workflow E2E روی SQLite in-memory)
Overall MVP status: کامل و قابل دمو با Mock AI
```
