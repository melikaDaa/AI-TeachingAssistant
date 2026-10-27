# مدرسه‌یار (MadreseAI) — دستیار هوشمند معلم

AI-powered teaching assistant for school teachers. A teacher enters basic lesson
information, chooses content types, and receives a complete **Teaching Package**:
lesson plan, educational content, classroom activity, quiz, image prompts, and a
video prompt — all in Persian, RTL-first.

> This MVP includes email/password sign-in with cookies. Password recovery,
> PDF upload, RAG, and direct image/video tool integrations are not available.

## Quick start

```bash
dotnet run --project src/AI.TeachingAssistant.Web
# → http://localhost:5000  (Persian RTL UI + REST API in one process)
```

Startup applies EF Core migrations to the configured SQLite database; it does not
seed a demo teacher or lessons. Startup can change the selected database schema.
For local UI data calls, configure `InternalAssertion:Secret` with User Secrets
or an environment variable (`InternalAssertion__Secret`). Text generation uses
an offline mock by default and needs no AI API key.

## Switching to a real AI provider

Configuration comes from environment variables; never commit secrets:

```bash
AI_PROVIDER=xkiro           # default: mock; `openai` is also accepted
AI_API_KEY=<secret>         # or XKIRO_API_KEY
# Optional: AI_BASE_URL, AI_MODEL, AI_FALLBACK_MODEL
```

The configured default Base URL, model, and optional fallback are in
`src/AI.TeachingAssistant.Api/appsettings.json`. A fallback is used only when
configured and the primary provider call fails transiently.

## Architecture

Clean Architecture, .NET 9, Blazor Web App (Interactive Server) + REST API:

```
src/
  AI.TeachingAssistant.Domain          Entities, enums, payload models
  AI.TeachingAssistant.Application    DTOs, IAIProvider, prompt builders,
                                      generation/validation services
  AI.TeachingAssistant.Infrastructure EF Core (SQLite), repositories,
                                      Mock + OpenAI-compatible providers,
                                      background generation worker
  AI.TeachingAssistant.Api            REST controllers, error middleware,
                                      rate limiting
  AI.TeachingAssistant.Web            Persian RTL Blazor UI
  AI.TeachingAssistant.Tests          xUnit test project
```

Key design points:

- **`IAIProvider`** is the only AI seam — swap providers without touching services.
- **Prompt builders** (`LessonPlanPromptBuilder`, `QuizPromptBuilder`, …) produce
  structured Persian prompts that request **strict JSON**; responses are parsed,
  **validated** (contiguous lesson timing, exactly 5 MCQ + 3 short answers with
  valid answer keys, 2 image prompts, …) and retried once before failing gracefully.
- **Sections are independent**: regenerating the quiz never touches other sections.
- **Generation runs in a background worker**; the API returns `202` and the UI
  polls progress. Durability depends on the implementation included in the
  particular source revision.
- **Errors never leak internals**: global middleware returns friendly Persian
  messages only; API keys stay server-side.

## REST API

```
POST   /api/lessons                     create lesson
GET    /api/lessons                     list lessons (recent first)
GET    /api/lessons/{id}                full Teaching Package DTO
PUT    /api/lessons/{id}                update lesson
DELETE /api/lessons/{id}                delete lesson + package (cascade)
POST   /api/lessons/{id}/generate       queue full-package generation (202)
POST   /api/lessons/{id}/generate/{section}   regenerate one section
PUT    /api/lessons/{id}/sections/{section}   save teacher edits of a section
GET    /api/teacher                     teacher profile
PUT    /api/teacher                     update profile
POST   /api/account/register            register and sign in
POST   /api/account/login               sign in
GET    /api/account/csrf                obtain antiforgery token
POST   /api/account/logout              sign out
GET    /api/usage                       current teacher's usage summary
GET    /api/images/{fileName}           retrieve an image (anonymous)
GET    /health/live                     process liveness
```

Lesson, teacher, and usage calls are scoped to the authenticated teacher.
Registration and login require an antiforgery token and are rate-limited. Image
reads are public to anyone holding a valid image URL; they are not cookie-protected.
Generated image files are stored under `App_Data/generated-images` beneath the
application base directory by default; `AI_IMAGE_STORAGE_ROOT` overrides this path.

`{section}` ∈ `lesson-plan | content | activity | quiz | image-prompt | video-prompt`

## Database

SQLite (default `Data Source=madreseai.db`; override with
`ConnectionStrings__Default`) for the MVP. Startup applies EF Core migrations
and does not seed demo records. Teacher → Lesson → TeachingPackage →
{LessonPlan, EducationalContent, ClassroomActivity, Quiz, MediaPrompt[]} with
cascade deletes; generated payloads are stored as JSON. PostgreSQL is not
implemented; a provider change requires separate schema and migration review.

## Tests

```bash
dotnet test
```

Covers application/service behavior, AI parsing/validation and provider fallback,
image generation/storage, authentication service behavior, and database, usage,
and queue behavior where those tests exist in the source revision. This does not
claim full HTTP-pipeline or browser E2E coverage.

## Demo scenario

1. Register or sign in, then open the dashboard → «ساخت محتوای جدید»
2. Enter: علوم / هفتم / سلول / 60 دقیقه / متوسط
3. Select «ساخت محتوای کامل کلاس» → «✨ تولید محتوا»
4. Watch the progress steps → six tabs appear
5. Open «آزمون» → «تولید مجدد این بخش» (only the quiz regenerates)
6. Open «پرامپت ویدئو» → «کپی متن» → paste into Google Flow
7. Back on the dashboard, the lesson appears in «درس‌های اخیر» with «آماده»
