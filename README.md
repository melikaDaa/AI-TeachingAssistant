# مدرسه‌یار (MadreseAI) — دستیار هوشمند معلم

AI-powered teaching assistant for school teachers. A teacher enters basic lesson
information, chooses content types, and receives a complete **Teaching Package**:
lesson plan, educational content, classroom activity, quiz, image prompts, and a
video prompt — all in Persian, RTL-first.

> This is the MVP. It intentionally avoids: chat UIs, marketplaces, analytics,
> auth systems, PDF upload, and direct image/video tool integrations.

## Quick start

```bash
dotnet run --project src/AI.TeachingAssistant.Web
# → http://localhost:5000  (Persian RTL UI + REST API in one process)
```

First run applies EF Core migrations automatically and seeds a demo teacher
(مریم احمدی — علوم، هفتم) with sample lessons. The AI provider defaults to a
deterministic offline mock, so the entire product works **without any API key**.

## Switching to a real AI provider

Configuration comes from environment variables (never committed, §27):

```bash
AI_PROVIDER=openai          # default: mock
AI_API_KEY=sk-...
AI_BASE_URL=https://api.openai.com/v1   # any OpenAI-compatible endpoint
AI_MODEL=gpt-4o-mini
```

Any OpenAI-compatible gateway works (OpenAI, Azure-style, Iranian providers).

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
  AI.TeachingAssistant.Tests          xUnit tests (23)
```

Key design points:

- **`IAIProvider`** is the only AI seam — swap providers without touching services.
- **Prompt builders** (`LessonPlanPromptBuilder`, `QuizPromptBuilder`, …) produce
  structured Persian prompts that request **strict JSON**; responses are parsed,
  **validated** (contiguous lesson timing, exactly 5 MCQ + 3 short answers with
  valid answer keys, 2 image prompts, …) and retried once before failing gracefully.
- **Sections are independent**: regenerating the quiz never touches other sections.
- **Generation runs in the background** (channel + hosted worker); the API returns
  `202` immediately and the UI polls the persisted progress.
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
```

`{section}` ∈ `lesson-plan | content | activity | quiz | image-prompt | video-prompt`

## Database

SQLite (`madreseai.db`) for the MVP. Teacher → Lesson → TeachingPackage →
{LessonPlan, EducationalContent, ClassroomActivity, Quiz, MediaPrompt[]} with
cascade deletes; generated payloads are stored as JSON. Moving to PostgreSQL
later only requires swapping the EF provider in `DependencyInjection.cs` and
regenerating migrations.

## Tests

```bash
dotnet test
```

Covers: AI JSON extraction (fences, nested braces), response validation rules,
input validation, section normalization, graceful AI-failure handling, and the
mock provider's output passing all validators.

## Demo scenario

1. Open the dashboard → «ساخت محتوای جدید»
2. Enter: علوم / هفتم / سلول / 60 دقیقه / متوسط
3. Select «ساخت محتوای کامل کلاس» → «✨ تولید محتوا»
4. Watch the progress steps → six tabs appear
5. Open «آزمون» → «تولید مجدد این بخش» (only the quiz regenerates)
6. Open «پرامپت ویدئو» → «کپی متن» → paste into Google Flow
7. Back on the dashboard, the lesson appears in «درس‌های اخیر» with «آماده»
