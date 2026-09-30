# Design System (Current State)

> استخراج‌شده از `src/AI.TeachingAssistant.Web/wwwroot/app.css` و فایل‌های Razor در تاریخ 2026-09-27. این توصیفِ وضعیت فعلی است، نه خواسته‌های آینده.

## Overview

CSS سفارشی خالص (بدون Tailwind/Bootstrap/کتابخانه کامپوننت). یک فایل واحد با CSS variables، کلاس‌های utility سبک و کامپوننت‌کلاس‌ها. سبک هدف: «Calm AI Education» — آبی برند + فضای سفید، بدون گرادیان سنگین.

## Colors (متغیرهای واقعی فعلی)

| Token | Value | استفاده فعلی |
| ----- | ----- | ------------- |
| `--primary` | `#2196F3` | CTA، تب فعال، nav فعال، دکمه اصلی، progress ring |
| `--primary-dark` | `#1a83d6` | hover دکمه primary |
| `--dark` | `#0D47A1` | H1/H2، برند، تب فعال‌قبلی pills، متون تأکیدی، select-all بنر |
| `--light` | `#E3F2FD` | hero، nav فعال، select-card انتخاب‌شده، time-chip، pill.current |
| `--soft` | `#90CAF9` | hover border، border hero، chip border، focus input |
| `--bg` | `#F7FAFC` | پس‌زمینه صفحه، prompt-card |
| `--surface` | `#FFFFFF` | کارت‌ها، nav، دکمه ghost |
| `--ink` | `#172033` | متن اصلی |
| `--muted` | `#64748B` | متن فرعی، placeholder |
| `--border` | `#E2E8F0` | border کارت‌ها/اینپوت‌ها |
| `--danger` / `--danger-light` | `#B3261E` / `#FDECEA` | فقط حذف/خطا |
| `--ok` / `--ok-light` | `#15803D` / `#E7F6EC` | وضعیت آماده، پیام موفقیت |
| `--amber` / `--amber-light` | `#B45309` / `#FEF3C7` | وضعیت در حال تولید، نکته ویرایش |
| `--radius` / `--radius-sm` | `14px` / `10px` | گردی کارت/دکمه |
| `--shadow` | `0 1px 2px + 0 6px 18px rgba(23,32,51,.05)` | سایه بسیار نرم کارت‌ها |
| alias قدیمی | `--accent`→primary، `--accent-dark`→dark، `--accent-light`→light | سازگاری با کلاس‌های legacy |

## Typography

- **Font family:** `Vazirmatn, "Segoe UI", Tahoma, sans-serif` — فیس‌فونت از CDN (`app.css` لینک در `App.razor` به jsDelivr v33.003)
- **Base:** body `15.5px` (موبایل `15px`)، `line-height: 1.9`
- **H1:** `1.7rem / 800 / --dark` (موبایل `1.4rem`) — H2: `1.25rem / --dark` — H3: `1.02rem / --ink`
- **Body/p:** `margin: .3rem 0` — **Button:** `font-weight: 700`، `.95rem` (big: `1.05rem`)
- لیبل فرم: `.93rem/700` — meta کارت‌ها: `.84rem` — badge/chip/pill: `.74–.82rem/700`

## Components (واقعی)

- **Buttons:** `.btn` (primary آبی، hover تیره‌تر، disabled opacity .55)، `.btn.ghost` (سفید + `--border`، hover آبی‌ملایم)، `.btn.danger` (فقط حذف)، `.btn.small` / `.btn.big` (موبایل: big عرض کامل)
- **Inputs:** `.field` + label bold؛ input/select/textarea با border `--border`، radius-sm؛ hover: `--soft`؛ focus: outline `--soft` + border `--primary`
- **Cards:** `.card` (سفید، border ظریف، radius 14، سایه نرم) — `.lesson-card` (کارت درس)، `.capability` (کارت‌های 💡 Home با آیکون‌باکس `--light`)، `.prompt-card`
- **Navbar:** `.top-nav` sticky سفید + `.brand` (🎓 در مربع `--light`، نام `--dark`، زیرعنوان muted) + `.nav-link` pill (active: `--light/--dark`) — بدون Sidebar
- **Tabs:** `.tab` pill؛ فعال = آبی پرِ متن سفید؛ نقطه ● وضعیت بخش (سبز=تولیدشده)
- **Badges:** `.badge.draft/generating/ready/failed` + `.chip` (خلاصه درس) + `.q-type`
- **Toast:** کلاس `.toast` تعریف شده — **در حال حاضر هیچ کامپوننتی از آن استفاده نمی‌کند** (feedbackها inline هستند: «کپی شد ✓»)
- **Modal:** **Not implemented** — حذف با `window.confirm` بومی مرورگر انجام می‌شود
- **Loading:** `.progress-ring` (اسپینر چرخان آبی) + `.progress-msg/sub` + `.progress-steps` با `.pill.done/current` (چک‌لیست دستیار)
- **Empty states:** `.empty` با dashed `--soft` + emoji + عنوان + CTA («هنوز کلاسی آماده نکرده‌اید 🌱»)
- **Wizard steps:** `.steps/.step` با `.n` دایره‌ای؛ active آبی، done سبز
- **Selector:** `.select-all` (بنر سرمه‌ای «✨ ساخت محتوای کامل کلاس» با هاله `--light` وقتی selected) + `.selector-grid` `.select-card` (selected: border آبی + پس‌زمینه `--light`)
- **Feedback:** `.edited-note` («کپی شد ✓» / «ذخیره شد»)، `.error-banner` قرمز ملایم، `.edit-note` کهربایی

## Layout

- **Container:** `.page` با `width: min(1080px, 100% - 2.4rem)` مرکزچین؛ padding عمودی `2rem/3rem`
- **Grid:** `.grid/.grid-2/.grid-3`، `.form-row` (۲ ستونه)، `.selector-grid` (۳ ستونه)، `.capabilities` (۴ ستونه)
- **Spacing:** فاصله پایه grid `~.9–1rem`، padding کارت `1.1–1.4rem`
- **Radius:** کارت 14px، دکمه/اینپوت 10px، pill/badge/chip 999px
- **Breakpoints:** `max-width: 860px` (گرید‌ها → ۲ ستون) و `max-width: 620px` (همه → ۱ ستون، nav شکسته، `.btn.big` full-width، font کوچک‌تر) — breakpoint بین 620 و 860 برای تبلت با گرید ۲ ستون پوشش داده می‌شود

## RTL Audit (نتیجه بررسی)

- Direction در یک نقطه تنظیم می‌شود: `<html lang="fa" dir="rtl">` در `App.razor` — کل UI (فرم‌ها، nav، کارت‌ها، تب‌ها، جدول‌های dl) RTL است
- CSS از propertyهای منطقی استفاده می‌کند (`margin-inline-start:auto` برای nav-links، `padding-inline-start` برای لیست‌ها) — RTL در تغییر سایز به هم نمی‌ریزد
- فرم‌ها RTL کامل؛ آیکون‌های emoji درون متن جریان RTL را حفظ می‌کنند (بدون absolute positioning)
- موبایل RTL بررسی و سالم تأیید شد (اسکرین‌شات ۳۹۰px در جریان توسعه)
- نکته جزئی: متن‌های انگلیسی پرامپت‌ها (FinalPrompt) در باکس LTR-محتوا هستند ولی باکس خودش RTL است؛ به‌علاوه `.payload-editor` آگاهانه `direction: ltr` است (JSON)

## Responsive Audit

| صفحه | Desktop | Tablet (~860px) | Mobile (~390px) |
| ---- | ------- | ---------------- | ---------------- |
| Home | ✅ ۴ ستون capabilities | ✅ ۲ ستون | ✅ ۱ ستون، hero جمع‌وجور |
| NewLesson | ✅ فرم ۲ ستونه | ✅ | ✅ تک‌ستون، دکمه‌ها full-width |
| Lessons | ✅ grid-2 کارت‌ها | ✅ | ✅ تک‌ستون |
| LessonDetail | ✅ تب‌ها wrap می‌شوند | ✅ | ✅ تب‌ها چندردیفی، بدون سرریز |
| Profile | ✅ ۲ ستونه فرم | ✅ | ✅ تک‌ستون |

مشکل شناخته‌شده فعلی: در صفحه‌های خیلی بلند، اسکرین‌شات fullPage با sticky nav آرتیفکت دارد (مربوط به ابزار اسکرین‌شات است، نه CSS؛ در مرورگر واقعی درست است).

## Design Gaps (Current vs Intended — بدون تغییر در این audit)

- **رنگ‌ها:** فعلی دقیقاً مطابق پالت هدف است (`#2196F3`, `#0D47A1`, `#E3F2FD`, `#90CAF9`, `#F7FAFC`, `#FFFFFF`, `#172033`, `#64748B`, `#E2E8F0`) — Gap رنگی وجود ندارد.
- **Modal:** هدف یک dialog داخلی فارسی برای حذف است؛ فعلاً `window.confirm` بومی استفاده می‌شود.
- **Toast:** کلاس CSS هست، wired نیست — فرصت استفاده برای پیام‌های موفقیت سراسری.
- **Iconography:** فعلاً همه‌چیز emoji است؛ هنوز icon library یکپارچه‌ای وجود ندارد (اگر اضافه شود باید ترکیب emoji+icon منظم شود).
- **Dark mode:** Not implemented (متغیرهای CSS ساختار لازم را دارند).
- **Focus state:** `:focus-visible` سراسری آبی است؛ فقط h1–h3 عمداً بدون ring (به‌خاطر FocusOnNavigate بلیزر).

## File References

- `src/AI.TeachingAssistant.Web/wwwroot/app.css` — کل سیستم
- `src/AI.TeachingAssistant.Web/Components/App.razor` — RTL + فونت + copyToClipboard JS
- `src/AI.TeachingAssistant.Web/Components/Layout/MainLayout.razor` — nav/brand/footer
- `src/AI.TeachingAssistant.Web/Components/Shared/*.razor` — کامپوننت‌کلاس‌های بخش‌ها
