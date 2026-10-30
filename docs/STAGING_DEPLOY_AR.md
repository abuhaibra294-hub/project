# نشر DareCare Staging عبر HTTPS — تعليمات فنية، غير منفذة على خدمة خارجية

**لا يوجد دومين أو سيرفر Dev متصل في بيئة إنشاء الحزمة. لم يحدث نشر خارجي ولا إنشاء SQL Server فعلي.** هذا إعداد قابل للتطبيق على VPS مع Docker Compose؛ الهدف بيانات وهمية فقط.

## المتطلبات قبل أمر النشر

1. VPS يدعم Docker Engine + Docker Compose، عليه IPv4 عام ومنافذ 80 و443 مفتوحة. **الاستضافة المشتركة PHP لا تكفي لتشغيل هذا Docker Compose**.
2. دومين فرعي مثل `api-staging.YOURDOMAIN` وسجل DNS `A` يشير إلى VPS، وواجهة الويب لها `https://web-staging.YOURDOMAIN`.
3. SQL Server خارجي بتشفير TLS ونسخة احتياطية، قاعدة فارغة اسمها `DareCareStaging`، وبيانات دخول بصلاحيات محدودة.
4. SMTP لإرسال أكواد تسجيل الحسابات؛ تأكيد البريد مفروض في Staging.
5. 48 بايت عشوائية على الأقل لـJWT Key؛ مثال توليدها على ويندوز PowerShell (لا تطبعها في سجل عام):
```powershell
$bytes = New-Object byte[] 48
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
[Convert]::ToBase64String($bytes)
```

## إعداد قاعدة البيانات (مرة أولى على جهاز المطور)

**تم توليد وحفظ EF Core Migration الخاصة بـSQL Server داخل المستودع في Sprint 8.** لا تولّد `InitialCreate` ثانية. يجب أن تكون القاعدة جديدة وفارغة، وأن تضبط `ConnectionStrings__DefaultConnection` في بيئة آمنة قبل تطبيق المخطط. شغّل:

```powershell
dotnet restore DareCare.slnx
dotnet tool restore
dotnet ef migrations script --idempotent --project src/DareCare.Infrastructure --startup-project src/DareCare.Api --output staging-migrations-review.sql
# راجع ملف SQL قبل التطبيق، ثم:
dotnet ef database update --project src/DareCare.Infrastructure --startup-project src/DareCare.Api
```

**لا تجرِ database update على قاعدة قديمة تحوي جداول موجودة بلا Migration History، أو على بيانات حقيقية دون نسخة احتياطية ومراجعة.** يُستخدم حساب ترحيل محدد الصلاحيات، ثم حساب تشغيل أقل صلاحيات. لم يتم إنشاء SQL Server خارجي على Render بهذا التعديل؛ انظر `docs/SPRINT8_SQLSERVER_AR.md`.

## تشغيل Docker + Caddy HTTPS

على VPS داخل مجلد المشروع:
```bash
cp deployment/.env.staging.example .env.staging
chmod 600 .env.staging
# عدّل .env.staging بمحرر خاص على السيرفر: الدومين، Origin، SQL Server، JWT، SMTP.
docker compose --env-file .env.staging -f deployment/compose.staging.yml up -d --build
curl -i https://api-staging.YOURDOMAIN/health/ready
curl -i https://api-staging.YOURDOMAIN/openapi/v1.json
```

Caddy يحصل على شهادة TLS تلقائيًا **فقط إذا كان DNS والمنافذ والدومين مضبوطين**. التطبيق نفسه غير مكشوف على منفذ HTTP عام؛ الوصول عبر Caddy. الملفات الطبية على Docker volume خاص مستمر بعد إعادة إنشاء الحاوية، لكنها **ليست تخزينًا طبيًا مؤمَّنًا للإنتاج**. يجب وضع سياسة نسخ احتياطي للـvolume ومفاتيح SQL خارج Git.

### Bootstrap Admin مرة واحدة (Staging فقط)

قبل أول تشغيل يمكن ضبط المتغيرات الآتية في `.env.staging`:
```
BootstrapAdmin__AllowOneTimeStaging=true
BootstrapAdmin__Email=ADMIN_TEST_EMAIL
BootstrapAdmin__Password=STRONG_RANDOM_PASSWORD
```

بعد نجاح الإنشاء والتأكد من أن الحساب يملك Admin، غيّر `AllowOneTimeStaging=false` واحذف بيانات الأدمن من `.env.staging` ثم أعد تشغيل الخدمة. هذا الاختيار غير متاح في Production، ولا يرفع صلاحيات أي حساب موجود مسبقًا.

## اختبارات القبول على رابط Staging

في مشروع مطور الـBackend شغّل:
```powershell
powershell -File scripts/Verify-TeamApi.ps1 -BaseUrl 'https://api-staging.YOURDOMAIN' -FrontendOrigin 'https://web-staging.YOURDOMAIN'
powershell -File scripts/Export-LiveApi.ps1 -BaseUrl 'https://api-staging.YOURDOMAIN'
```
- `GET /health/ready` يجب أن يعيد `200`؛ غير ذلك لن يبدأ الفريق بالتكامل.
- `GET /openapi/v1.json` يظهر فقط حين `ApiDocs:EnableInStaging=true`؛ في Production لا يظهر.
- مصدر Postman للمطورين هو `integration/DareCare_Postman_Collection.json`؛ عدّل Base URL في Postman Environment الخاصة بكل فرد.
- للمطورين يمكن استخدام حسابات staging وهمية معدَّة يدويًا عبر التدفق العادي، بعد وجود الأدمن وSMTP. `PREPARE_TEAM_DEMO.cmd` يتعامل مع **النسخة المحلية فقط**، ولا يصلح لـ Staging لأنه يعتمد على صندوق بريد Loopback/Development.

## قيود لا يمكن تجاوزها

- لا ترفع `ASPNETCORE_ENVIRONMENT=Development` على دومين عام؛ صفحة `/dev/auth` وصندوق البريد التجريبي مخصصان لـlocalhost.
- لا تسمح بأصل `*` في CORS، ولا تستخدم HTTP على إنترنت عام.
- لا تحفظ JWT أو Passwords في GitHub أو Postman Collections المشتركة.
- لا تدخل روشتات أو معلومات مرضى حقيقية قبل المراجعة الأمنية والقانونية، ومسح الملفات من البرمجيات الخبيثة، وتشفير مناسب، وإدارة موافقة المريض، وحوكمة الصيدليات والتمريض.
- أصلح Build وUnit Tests وSmoke Tests أولًا؛ إعداد النشر لا يثبت أن الخدمة اشتغلت.

> Render alternative: read `docs/RENDER_DEPLOY_AR.md` and `render.yaml` before using a provider.
