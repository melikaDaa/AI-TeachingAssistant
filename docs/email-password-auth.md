# Email/password authentication (MVP)

The MVP uses the existing `Teachers` row as the account and teacher profile. The stable `Teacher.Id` remains the identity used by lessons and the Web/API HMAC assertion. New accounts require a valid email and password; the email is normalized to lowercase and checked case-insensitively. Existing legacy teacher rows and lessons are preserved, but no email or password is fabricated for them. The already-present OTP migration deactivated rows without a phone number; this email/password change does not reactivate them or migrate ownership. Legacy account recovery/activation needs an explicit verified-email or support process before those rows can sign in.

Password hashes use ASP.NET Core Identity's `PasswordHasher<Teacher>`. Cookies are HTTP-only, secure, same-site strict, and limited to 14 days. Login and registration are rate limited and require an antiforgery request token. The server-side API bridge continues deriving `teacher_id` only from the authenticated cookie and signs its short-lived HMAC assertion; callers cannot choose another teacher ID.

No migration is needed for the email/password change: the existing `PasswordAuthentication` migration already added nullable `Email` and `PasswordHash` columns and a unique email index without deleting Teachers or lessons. At startup normal EF migrations are applied. No SMS credentials, Kavenegar template, or OTP secret is needed.

Automatic demo teacher/lesson seeding is disabled in both hosts so new development installations do not share a default account. Existing demo data is not deleted.

## Run locally

Set the Web/API bridge secret through User Secrets (never commit it):

```powershell
dotnet user-secrets set "InternalAssertion:Secret" "<long-random-secret>" --project src/AI.TeachingAssistant.Web/AI.TeachingAssistant.Web.csproj
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/AI.TeachingAssistant.Web/AI.TeachingAssistant.Web.csproj --urls http://localhost:5000
```

Visit `/account/register` to create an email account. It signs in on success. `/account/login` accepts that email/password; the header includes sign out. `/account` redirects to email login. Automatic password reset remains disabled until a secure delivery flow is designed.

## Deferred SMS login

Historical OTP sender/service and challenge migration code are intentionally retained but unregistered and unreachable through the account API. Before reactivation, add phone verification UX, verified phone-to-account linking (do not create an unlinked duplicate Teacher), register the provider behind `IPhoneOtpSender`, add endpoint rate limits and CSRF controls, and test cookie issuance and ownership. Kavenegar's former VerifyLookup contract and setup notes can be recovered from repository history; SMS.ir should not be reintroduced as a second active provider.
