# Deployment and production-readiness notes

## Current deployment shape

The root `Dockerfile` publishes `src/AI.TeachingAssistant.Web/AI.TeachingAssistant.Web.csproj`. This is the production host: it serves the Blazor UI and API controllers in one process. It listens on container port 80 and is configured for `Production`.

## Access control

The app now has email/password authentication (cookie-based; see `docs/email-password-auth.md`). Every lesson/profile operation is scoped to the signed-in teacher through the server-side Web→API bridge. Remaining limitations: there is no account recovery flow yet, image reads (`/api/images/{file}`) are anonymous, and the login is MVP-level — do not treat it as a hardening boundary for sensitive data. Restrict ingress appropriately until multi-user hardening is designed.

## Demo-data initialization

EF migrations run at startup and can change the schema of the configured database. The current `DatabaseInitializer` does not insert demo records, and the reviewed startup code does not consume `DemoData:Seed`. Existing records are not deleted by this initializer.

## Liara manual configuration checklist

The Dockerfile defaults expect all of the following to be checked in the Liara dashboard; the Dockerfile's `mkdir` does **not** make data persistent by itself:

1. Attach a Liara persistent Disk and mount it at exactly `/app/data`.
2. Confirm the mounted directory is writable by the container's non-root `app` user (UID/permissions must allow SQLite files, SQLite WAL/SHM files, and generated image creation).
3. Verify the disk contents survive both an application restart and a new deployment/replacement container. Keep one application instance; SQLite is not configured for concurrent replicas or network filesystems.
4. Keep `ConnectionStrings__Default=Data Source=/app/data/madreseai.db` and `AI_IMAGE_STORAGE_ROOT=/app/data/generated-images`, or set equivalent paths on the same mounted disk. Existing image URLs (`/api/images/{file}`) remain unchanged.
5. Set `InternalAssertion__Secret` to a long random value (dashboard secret/environment). This is the shared HMAC secret of the in-process Web→API bridge; if it is missing, login still works but every UI data call (profile, lessons, generation) fails with 401/500. `InternalApi__BaseUrl` is not needed: it defaults to the container's own loopback port 80.
6. Configure Liara's target/container port as `80` and expose the app only on the intended public route (the app now has email/password authentication, but treat it as an MVP-level login, not a hardening boundary).
7. Set `ASPNETCORE_ENVIRONMENT=Production`. Do not rely on `DemoData__Seed` for this revision; no seed behavior is implemented in the reviewed initializer.

The database and image paths are configurable through `ConnectionStrings__Default` and `AI_IMAGE_STORAGE_ROOT`. The Docker defaults point to `/app/data`; outside Docker the existing development defaults remain local to the app.

## AI environment configuration

The application reads these environment variables (exact names are significant):

- `AI_PROVIDER=xkiro` to select the OpenAI-compatible real text provider (`openai` is also supported).
- `AI_API_KEY` or `XKIRO_API_KEY` for the text-provider secret. Keep it only in Liara's secret/environment settings.
- Optional text settings: `AI_BASE_URL`, `AI_MODEL`, `AI_FALLBACK_MODEL`.
- Optional image generation: `AI_IMAGE_PROVIDER=xkiro`, `AI_IMAGE_API_KEY` (falls back to the text key), `AI_IMAGE_BASE_URL`, `AI_IMAGE_MODEL`, `AI_IMAGE_SIZE`, and `AI_IMAGE_STORAGE_ROOT`.
- Database: `ConnectionStrings__Default`.

Text generation defaults to the mock provider and image generation defaults to disabled. Therefore, if real text AI is intended, explicitly set `AI_PROVIDER=xkiro` plus a secret key; do not infer provider activation from having configured a key. No credential should be placed in source, Docker build arguments, committed appsettings, or frontend configuration.

## Health and diagnostics

`GET /health/live` is a lightweight process liveness check and returns HTTP 200 with `{ "status": "ok" }`. It intentionally does not test SQLite, storage, or the external AI provider and must not be treated as readiness or proof that generation works. Startup migration errors are process-startup failures and should be diagnosed from restricted hosting logs. AI error logs contain upstream status/timing but not upstream response bodies or credentials; user-facing errors remain generic Persian messages.

No Docker `HEALTHCHECK` is declared because the runtime image does not guarantee a probe executable. Configure a Liara HTTP liveness probe for `/health/live` if supported; otherwise monitor this endpoint externally. Do not expose detailed readiness/configuration or secret information.

## Accepted MVP operational limitations

- Generation jobs are persisted in SQLite and reclaimed after lease expiry; SQLite remains a single-host/single-file MVP store and is not guaranteed on network disks or multiple hosts. A request already sent to an external provider cannot be cancelled by fencing and may be repeated after a crash; its outcome/cost can be unknown.
- SQLite and local image files require a verified persistent writable Liara disk and backups. This repository cannot verify the remote mount, persistence, permissions, or backup policy.
- Cookie authentication and teacher-scoped lesson/profile calls exist in the reviewed code, but this does not establish security hardening or scale-out readiness. Image reads remain public to anyone with a valid URL; confirm this exposure is acceptable for deployed data.

## Runtime support

The projects and Docker stages target .NET 9. Microsoft's support policy lists .NET 9 as STS with end of support on **November 10, 2026**. Schedule a separate framework/runtime upgrade before that date; do not deploy an unsupported runtime after support ends. Source: [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
# Usage Accounting و Durable Generation

## سهمیه و Rate Limit

- `Usage:DailyTextUnits` و `Usage:DailyImageUnits` سقف‌های policy قابل تنظیم‌اند؛ این‌ها قیمت یا هزینهٔ پولی Provider نیستند. مقادیر پیش‌فرض به‌ترتیب ۱۲۰ و ۲۴ واحد باقی می‌مانند.
- `Usage:TextSectionUnits`، `Usage:RegenerateUnits` و `Usage:ImageUnits` وزن سهمیه را تعیین می‌کنند. پاسخ rate-limit کوتاه‌مدت (درخواست در دقیقه) مستقل از سهمیه روزانه است.
- Usage واقعی توکن فقط وقتی provider مقدار `usage` بدهد قابل ثبت است. در نبود داده، مقدار توکن و هزینهٔ پولی را واقعی یا صفر فرض نمی‌کنیم؛ وضعیت هزینه نامعلوم می‌ماند.

## Job، lease و تنظیمات

- `Usage:ReservationLeaseMinutes` زمان lease برای job و reservation (پیش‌فرض ۱۲۰ دقیقه) و `Usage:LeaseHeartbeatSeconds` فاصلهٔ heartbeat (پیش‌فرض ۳۰ ثانیه) است.
- `Usage:ReconciliationIntervalMinutes` فاصلهٔ reconciliation (پیش‌فرض ۵ دقیقه) است. `UsageOptions` هنگام startup به singleton تبدیل می‌شود؛ تغییر این تنظیمات نیازمند restart است.
- Job پذیرفته‌شده در جدول `GenerationJobs` پایدار می‌شود. Worker با SQLite write transaction آن را claim می‌کند، lease را heartbeat می‌کند و fencing token را هنگام claim و finalization بررسی می‌کند.
- Shutdown کنترل‌شده تلاش می‌کند job را به Pending برگرداند و رزروهای باقی‌مانده را آزاد کند. در crash ناگهانی، job پس از lease expiry قابل claim مجدد است. فراخوانی Provider خارجی ممکن است پیش از crash انجام شده باشد؛ پس از آن نتیجه/هزینهٔ خارجی می‌تواند نامعلوم باشد و retry ممکن است provider را دوباره فراخوانی کند.

## Migration و استقرار

- قبل از استقرار، از فایل SQLite نسخهٔ پشتیبان بگیرید و `dotnet ef database update --project src/AI.TeachingAssistant.Infrastructure --startup-project src/AI.TeachingAssistant.Api` را فقط روی فایل هدف اجرا کنید.
- زنجیرهٔ migration را ابتدا روی کپی آزمایشی دیتابیس نسخهٔ قبلی و نیز دیتابیس تازه اعتبارسنجی کنید؛ فایل production برای تست استفاده نشود.
- صف به SQLite و فایل مشترک وابسته است. SQLite برای یک میزبان/فایل محلی و نرخ نوشتن محدود طراحی شده؛ فایل‌سیستم شبکه‌ای و استقرار چندمیزبانه بدون قفل‌گذاری/ذخیره‌ساز سازگار تضمین‌شده نیست. fencing در DB از ثبت نهایی stale worker جلوگیری می‌کند، ولی اجرای خارجی را لغو نمی‌کند.

## بررسی عملیاتی

Jobهای `Failed` و reservationهای منقضی را در DB بررسی کنید. اگر Job بعد از provider-call و قبل از ذخیره نتیجه crash کند، وضعیت نتیجهٔ خارجی نامعلوم است؛ هزینه یا موفقیت را صفر/واقعی فرض نکنید. سهمیهٔ تصویر نیز واحد policy است و هزینهٔ واقعی Provider محسوب نمی‌شود.
