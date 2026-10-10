# Design System (Redesigned)

> بازطراحی‌شده در تاریخ 2026-10-08. سیستم طراحی «AI-First Educational Assistant».

## Overview

CSS سفارشی خالص (بدون Tailwind/Bootstrap/کتابخانه کامپوننت). یک فایل واحد با CSS variables، کلاس‌های utility سبک و کامپوننت‌کلاس‌ها. سبک هدف: «AI-First Educational Assistant» — آبی برند + بنفش AI + فضای سفید، بدون گرادیان سنگین.

## Colors

| Token | Value | استفاده |
| ----- | ----- | ------- |
| `--primary` | `#2B4BD8` | CTA، تب فعال، nav فعال، دکمه اصلی |
| `--primary-dark` | `#1E3A9E` | hover دکمه primary |
| `--primary-light` | `#4A6AE8` | border hover، active states |
| `--primary-50` | `#EEF2FF` | پس‌زمینه ملایم، selected states |
| `--primary-100` | `#E0E7FF` | پس‌زمینه ملایم‌تر |
| `--secondary` | `#7C3AED` | AI accent، دکمه‌های ثانویه |
| `--secondary-dark` | `#5B21B6` | hover دکمه secondary |
| `--secondary-50` | `#F5F0FF` | پس‌زمینه ملایم secondary |
| `--accent` | `#6366F1` | AI glow، highlight |
| `--accent-glow` | `rgba(99,102,241,.15)` | هاله نرم AI |
| `--bg` | `#F8F9FC` | پس‌زمینه صفحه |
| `--surface` | `#FFFFFF` | کارت‌ها، nav |
| `--surface-2` | `#F1F3F9` | پس‌زمینه ثانویه |
| `--ink` | `#0F172A` | متن اصلی |
| `--body` | `#1E293B` | متن بدنه |
| `--muted` | `#64748B` | متن فرعی |
| `--faint` | `#94A3B8` | متن کم‌رنگ |
| `--border` | `#E2E8F0` | border کارت‌ها/اینپوت‌ها |
| `--border-strong` | `#CBD5E1` | border قوی‌تر |
| `--success` | `#059669` | وضعیت موفق |
| `--warning` | `#D97706` | وضعیت هشدار |
| `--danger` | `#DC2626` | وضعیت خطا |
| `--info` | `#0284C7` | وضعیت اطلاعات |

## Typography

- **Font family:** `Vazirmatn, "Segoe UI", Tahoma, sans-serif`
- **Base:** body `15px` (موبایل `14px`)، `line-height: 1.85`
- **H1:** `1.875rem / 800` (موبایل `1.5rem`)
- **H2:** `1.5rem / 800` (موبایل `1.25rem`)
- **H3:** `1.125rem / 700`
- **H4:** `1rem / 700`
- **Body/p:** `margin: .3rem 0`
- **Button:** `font-weight: 700`، `.95rem` (big: `1.05rem`)
- **Label فرم:** `.93rem/700`
- **Meta:** `.84rem`
- **Badge/chip/pill:** `.75–.82rem/700`

## Components

- **Buttons:** `.btn` (primary آبی)، `.btn.ghost` (سفید + border)، `.btn.danger` (قرمز)، `.btn.small` / `.btn.big`، `.btn.accent` (بنفش)، `.btn.ai` (gradient آبی-بنفش با shimmer)
- **Inputs:** `.field` + label bold؛ input/select/textarea با border `--border-strong`، radius 12px؛ focus: border `--primary` + box-shadow `--primary-50`
- **Cards:** `.card` (سفید، border ظریف، radius 16px، shadow نرم) — `.ai-output-card` (با gradient border بالا)
- **Sidebar:** `.sidebar` (عرض 260px، سفید، border چپ) — `.side-link` (radius 12px، active: `--primary-50`)
- **Top Header:** `.top-header` (sticky، backdrop-filter blur، سفید شفاف)
- **Tabs:** `.tab` pill؛ فعال = آبی پرِ متن سفید
- **Badges:** `.badge.draft/generating/ready/failed` + `.chip` (خلاصه درس) + `.q-type`
- **AI Components:** `.ai-badge` (gradient آبی-بنفش)، `.ai-suggestion` (پیام دستیار)، `.ai-output-card` (کارت خروجی AI)، `.btn-regenerate` (دکمه تولید مجدد)
- **Progress:** `.progress-mark` (انیمیشن pulse) + `.progress-msg/sub` + `.progress-steps` با `.pill.done/current`
- **Empty states:** `.empty` با dashed border + تصویر دستیار + عنوان + CTA
- **Error:** `.error-banner` قرمز ملایم با border
- **Toast:** `.toast` با انیمیشن slide-up
- **Steps:** `.steps/.step` با `.n` دایره‌ای؛ active آبی، done سبز
- **Selector:** `.select-all` (بنر انتخاب همه) + `.selector-grid` `.select-card` (selected: border آبی + پس‌زمینه `--primary-50`)

## Layout

- **Container:** `.page` با `width: min(1120px, 100% - 3.2rem)` مرکزچین
- **Grid:** `.grid/.grid-2/.grid-3`، `.form-row` (۲ ستونه)، `.selector-grid` (۳ ستونه)
- **Spacing:** فاصله پایه grid `1rem`، padding کارت `1.25–1.5rem`
- **Radius:** کارت 16px، دکمه/اینپوت 12px، pill/badge/chip 999px
- **Breakpoints:** `max-width: 900px` (Sidebar به Drawer آف‌کانواس با دکمه منو + overlay تبدیل می‌شود؛ تنها الگوی ناوبری موبایل) و `max-width: 620px` (همه تک‌ستون)

## AI Assistant

- **Brand Symbol:** `assets/brand-symbol.svg` — کتاب باز + جرقه AI
- **Assistant Character:** `assets/assistant-idle.svg` (حالت عادی)، `assets/assistant-thinking.svg` (در حال فکر)، `assets/assistant-success.svg` (موفق)، `assets/assistant-error.svg` (خطا)
- **Favicon:** `favicon.svg` — همان brand symbol

## File References

- `src/AI.TeachingAssistant.Web/wwwroot/app.css` — کل سیستم
- `src/AI.TeachingAssistant.Web/Components/App.razor` — RTL + فونت + favicon
- `src/AI.TeachingAssistant.Web/Components/Layout/MainLayout.razor` — sidebar + header + nav
- `src/AI.TeachingAssistant.Web/Components/Shared/BrandMark.razor` — نماد برند
- `src/AI.TeachingAssistant.Web/Components/Shared/Icon.razor` — آیکون‌ها
- `src/AI.TeachingAssistant.Web/Components/Pages/` — صفحات
- `src/AI.TeachingAssistant.Web/Components/Shared/` — کامپوننت‌های اشتراکی
