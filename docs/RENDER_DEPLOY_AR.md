# DareCare — نشر Staging على Render بدل Railway

## الحالة

**الحزمة جاهزة للنشر ولكن لم تُنشر.** لم يتم ربط حساب Render أو إعداد قاعدة SQL Server خارجية أو SMTP حقيقي أو توفير GitHub Repository خاص بـ DareCare. الرابط `*.onrender.com` لا يُنشأ إلا بعد فعل النشر من حسابك.

## مهم قبل البدء

- هذه Blueprint في `render.yaml` لخدمة **Render Web Service مدفوعة (starter) + Persistent Disk مدفوع**. لا تفترض أن النشر مجاني.
- قاعدة البيانات **SQL Server خارجية**: Render Postgres لا تعمل مع `UseSqlServer` بدون تعديل شامل للـProvider والـMigrations. لا تستخدم SQLite المعزول للتجربة كقاعدة عامة على الخدمة العامة.
- تستعمل API منفذ `8080` و`/health/ready` للفحص؛ Render يولد تلقائيًا رابط HTTPS من نوع `https://<your-service>.onrender.com` بعد نجاح النشر.
- الملفات المرفوعة تحفظ تحت `/data/prescriptions` بقرص دائم منفصل. يجب تجهيز نسخ احتياطية؛ وجود القرص لا يغني عن سياسة حماية بيانات.

## خطوات النشر الصحيحة

1. افتح المشروع محليًا بـ `START_DARECARE.cmd`. لازم `dotnet build` و`dotnet test` و`VERIFY_TEAM_API.cmd` تنجح **فعليًا**. لا تنشر مشروعًا لم يُختبر.
2. أنشئ **مستودع GitHub جديدًا خاصًا** باسم `DareCare.Backend` (لا تستخدم مستودع `project` أو مستودعات تحفيظم)، وارفع محتويات مجلد `DareCare.Backend` فقط. لا ترفع `.localdata/`, `App_Data/`, `.env.staging`, أي توكنات أو كلمات مرور أو معلومات مرضى.
3. جهّز SQL Server خارجيًا بدعم TLS، وصلاحيات DB مناسبة. من بيئة تطوير آمنة متصلة به أنشئ EF Migrations أو طبّق الموجودة (بعد مراجعة SQL والنسخ الاحتياطي). هذا ضروري **قبل** تشغيل الخدمة لأول مرة؛ السيرفر لا يهاجر SQL تلقائيًا.
4. جهّز خدمة SMTP حقيقية لتأكيد البريد، وبريدًا تجريبيًا للإدارة. احرص أن الحسابات التجريبية لا تستخدم معلومات حقيقية.
5. افتح https://dashboard.render.com/ → New → Blueprint، اختر مستودع DareCare الذي يحتوي على `render.yaml` في الجذر. النشر التلقائي مضبوط على `checksPass`، فلا تنشر التحديثات إلا بعد مرور GitHub CI بنجاح.
6. أدخل الحقول السرية المطلوبة في إعدادات Render: `ConnectionStrings__DefaultConnection`, `Cors__AllowedOrigins__0`, حقول SMTP. لا تضعها في Git.
7. لإنشاء أول مدير على Staging فقط، اضبط مؤقتًا في لوحة Render: `BootstrapAdmin__AllowOneTimeStaging=true` و`BootstrapAdmin__Email` و`BootstrapAdmin__Password` بقيم قوية. بعد أول نشر ناجح غيّر التفعيل إلى false **واحذف كلمات المرور من لوحة البيئة** ثم أعد النشر.
8. افتح `https://<render-url>/health/ready` وتأكد من HTTP 200، ثم `https://<render-url>/docs` لعرض المسارات و`https://<render-url>/openapi/v1.json` لإصدار OpenAPI من التطبيق الحي.
9. من جهازك اختبر عبر PowerShell:

```powershell
powershell -NoProfile -File scripts/Verify-TeamApi.ps1 -BaseUrl 'https://YOUR-SERVICE.onrender.com' -FrontendOrigin 'https://YOUR-FRONTEND.example'
powershell -NoProfile -File scripts/Export-LiveApi.ps1 -BaseUrl 'https://YOUR-SERVICE.onrender.com'
```

10. بعد نجاح الاختبارات، غيّر `baseUrl` في `integration/DareCare_Local.postman_environment.json` محليًا إلى الرابط الفعلي HTTPS، وسلّم Postman Collection وملف الـOpenAPI للفريق **بدون كلمات مرور أو Access Tokens**. قم بإنشاء الحسابات التجريبية الخاصة بكل دور، وأرسل البيانات بشكل خاص منفصل.

## CORS وحماية الوصول

- في Production لا توجد Origins افتراضية؛ أضف فقط HTTPS Origins الفعلية من لوحة Render. `appsettings.Development.json` وحده يحتوي على Origins المحلية.
- إعداد Staging يتيح `/docs` و`/openapi/v1.json` لقراءة الـContract، لكنه **لا يفتح** `/dev/auth` ولا `/auth.html` (المخصصين لـ Development). أوقف `ApiDocs__EnableInStaging` بعد الانتهاء إن أردت إخفاء التوثيق.
- لا تستخدم `ASPNETCORE_ENVIRONMENT=Development` على الإنترنت تحت أي ظرف.
- التحقق من البريد الإجباري وعدم ظهور كلمة المرور في الـSource شروط لازمة، ولكن **هذا ما زال MVP غير مؤهل لبيانات طبية حقيقية**.
- لا تختبر ملفات روشتات أصلية؛ استخدم PDF صغيرًا وهميًا بلا معلومات حساسة.

## ماذا لو أردت استضافة مختلفة؟

`Dockerfile` يشتغل مع أي خدمة تدعم Docker وSQL Server خارجيًا، ويمكن استخدام `deployment/compose.staging.yml` على VPS مع DNS وCaddy وشهادات TLS؛ فلا يوجد أي ارتباط إجباري بـRailway أو Render.
