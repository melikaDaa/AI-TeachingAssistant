# Product

## Name

مدرس‌یار (MadreseAI)

## Tagline

دستیار هوشمند معلم

## Target Users

- معلمان مدارس ایران (ابتدایی، متوسطه اول، متوسطه دوم — ثابت‌های `TeachingLevels`)
- ثبت‌نام و ورود ایمیل/گذرواژه در کد فعلی وجود دارد؛ Seed خودکار معلم دمو در startup انجام نمی‌شود. ممکن است دیتابیس‌های قدیمی داده دمو داشته باشند.

## Problem

تهیه طرح درس، محتوای آموزشی، فعالیت کلاسی، آزمون و پرامپت‌های رسانه‌ای برای هر جلسه کلاس ساعت‌ها وقت معلم را می‌گیرد و معمولاً در ابزارهای جدا و غیریکپارچه انجام می‌شود.

## Core Value Proposition

معلم فقط مشخصات کلاس (درس، پایه، موضوع، مدت، سطح) را وارد می‌کند و مدرس‌یار یک «بسته آموزشی» منسجم به زبان فارسی — متناسب با پایه و سطح دانش‌آموزان — تولید می‌کند؛ معلم نتیجه را بازبینی، ویرایش و بخش‌به‌بخش اصلاح می‌کند و برای کلاس استفاده می‌کند.

## Current MVP

- فرم ساخت درس با فیلدهای: عنوان، نام درس، پایه، موضوع، مدت کلاس (۱۵–۲۴۰ دقیقه)، سطح دانش‌آموزان (مبتدی/متوسط/پیشرفته)، توضیحات تکمیلی (اختیاری) و انتخاب نوع محتوا (۶ بخش یا «ساخت محتوای کامل کلاس»)
- تولید با **MockAIProvider** آفلاین (بدون کلید، بدون هزینه) با خروجی فارسی topic-aware و معتبر
- بسته آموزشی شامل تا ۶ بخش: طرح درس، محتوای آموزشی، فعالیت کلاسی، آزمون (۵ چهارگزینه‌ای + ۳ کوتاه + پاسخ‌نامه با توضیح)، ۲ پرامپت تصویر، ۱ پرامپت ویدئو
- تولید پس‌زمینه‌ای با progress زنده، ویرایش و ذخیره، تولید مجدد ایزوله هر بخش، کپی هر بخش/پرامپت، تاریخچه درس‌ها با حذفِ دارای تأیید

## Current User Journey

مسیر UI مورد انتظار و پیاده‌شده؛ این عبارت به‌تنهایی ادعای پوشش تست E2E مرورگر نیست:

```text
Home (/)
→ «✨ ساخت محتوای جدید» → /lessons/new
→ مرحله ۱: «✨ برای کدام کلاس محتوا می‌سازیم؟» — فرم اطلاعات کلاس (با validation فارسی سمت UI و Backend)
→ مرحله ۲: «🎯 چه چیزی برای کلاس شما آماده کنم؟» — انتخاب بخش‌ها یا «✨ ساخت محتوای کامل کلاس»
→ «✨ ساخت محتوای کلاس» → ایجاد درس + صف تولید → هدایت به /lessons/{id}
→ صفحه درس: «✨ مدرس‌یار در حال آماده‌سازی کلاس شماست» + چک‌لیست مراحل (polling هر ۱.۵ ثانیه)
→ «🎉 محتوای کلاس شما آماده است» + چیپ‌های خلاصه (درس/پایه/موضوع/مدت) + تب‌های ۶گانه
→ مشاهده / 📋 کپی / ✏️ ویرایش+ذخیره / 🔄 تولید مجدد هر بخش
→ «→ درس‌های من» → /lessons (کارت‌های درس با وضعیت و حذفِ دارای تأیید)
→ بازگشایی مجدد درس → همه محتواها از DB (persistence)
```

## Current Features

| Feature | Status | Location |
| ------------------------ | ---------------------------------------- | -------- |
| Home (داشبورد دستیاری) | Implemented | `src/AI.TeachingAssistant.Web/Components/Pages/Index.razor` |
| Create Lesson (فرم + ویزار ۲ مرحله‌ای) | Implemented | `.../Pages/NewLesson.razor` |
| Lesson List (کارت‌ها + وضعیت + حذف با تأیید) | Implemented | `.../Pages/Lessons.razor` |
| Teaching Package (تب‌ها + چیپ‌ها + بنر آماده) | Implemented | `.../Pages/LessonDetail.razor` + `.../Shared/TeachingPackageView.razor` |
| Lesson Plan | Implemented (زمان‌بندی جمعاً = مدت کلاس، validator دارد) | `.../Shared/LessonPlanView.razor` |
| Educational Content | Implemented | `.../Shared/EducationalContentView.razor` |
| Activity | Implemented | `.../Shared/ActivityView.razor` |
| Quiz (۵ + ۳ + پاسخ‌نامه با Explanation) | Implemented | `.../Shared/QuizView.razor` |
| Image Prompt (۲ عدد + کپی per-prompt) | Implemented | `.../Shared/PromptCard.razor` |
| Video Prompt (۱ عدد + کپی per-prompt) | Implemented | `.../Shared/PromptCard.razor` |
| Generation Progress (چک‌لیست زنده) | Implemented | `TeachingPackageView.razor` + `Web/Extensions/SectionMeta.cs` |
| Copy (بخش‌ها + پرامپت‌ها، «کپی شد ✓») | Implemented | `TeachingPackageView.razor`, `PromptCard.razor`, `Web/Extensions/TextExport.cs` |
| Editing (JSON با validation سمت سرور) | Implemented (بخش‌های ۱–۴) | `Application/Services/PackageSectionService.cs` |
| Regeneration (ایزوله هر بخش) | Implemented | `POST /api/lessons/{id}/generate/{section}` |
| Saving (ذخیره خودکار پس از تولید) | Implemented | `Application/Services/GenerationCoordinator.cs` |
| Delete (با تأیید مرورگر) | Implemented | `Lessons.razor` + `DELETE /api/lessons/{id}` |
| AI Provider abstraction | Implemented | `Application/Common/IAIProvider.cs` |
| Mock AI Provider | Implemented (پیش‌فرض) | `Infrastructure/Ai/MockAIProvider.cs` |
| OpenAI-compatible Provider | Implemented in code; انتخاب با `AI_PROVIDER=xkiro` یا `openai` و نیازمند API key | `Infrastructure/Ai/OpenAiCompatibleProvider.cs` |
| Partial-failure retry per section | Partially implemented (بخش‌های آماده سالم می‌مانند و از تب همان بخش «✨ ساخت همین بخش» ممکن است؛ badge اختصاصی «تولید نشد» در UI نیست) | `TeachingPackageView.razor` |
| Profile (نام/مقطع/پایه/دروس/سبک تدریس) | Implemented | `.../Pages/Profile.razor` |
| Email/password authentication | Implemented in current code; cookie-based registration/login; recovery is disabled | `Api/Controllers/AccountController.cs` |
| RAG / Knowledge Base | Not implemented | — |
| Multi-teacher / Multi-tenancy | Not implemented | — |

## Current Limitations

- هوش واقعی مصنوعی در اجرای عادی نیست؛ خروجی‌ها Mock هستند (ساختار و اعتبارسنجی واقعی، محتوا الگویی topic-aware)
- درباره durability صف، این سند وضعیت نسخه قابل انتشار را ادعا نمی‌کند؛ implementation و migrationهای محلی باید پیش از انتشار جداگانه تأیید شوند.
- ویرایش پرامپت‌های تصویر/ویدئو از مسیر ویرایش JSON پشتیبانی نمی‌شود (فقط کپی)
- بازیابی خودکار گذرواژه فعال نیست. وجود login و teacher-scoping به‌تنهایی تضمین hardening کامل یا پشتیبانی scale-out نیست.
- تک‌نمونه (single-instance) — صف و WebSocket مکالمه Blazor برای scale-out مناسب نیستند

## Future Direction

- بهبودهای Provider واقعی و مدیریت هزینه/توکن، بر اساس نیاز محصول و تنظیمات استقرار
- Structured Output سخت‌گیرانه‌تر و کنترل token/cost
- RAG و دانش‌نامه معلم (کتاب درسی، جزوات)
- بازیابی امن حساب، hardening چندکاربره و PostgreSQL
- badge «تولید نشد» + دکمه retry اختصاصی در تب‌ها (تکمیل partial-failure UI)
