# DareCare Sprint 8 — SQL Server Persistent Staging

## ما الذي نُفّذ؟
- توليد EF Core Migration ثابتة باسم `20261030184459_InitialSqlServer` من نموذج SQL Server الحقيقي، مع Snapshot داخل المستودع.
- فصل قاعدة SQL Server عن SQLite التجريبية على Render Free. الـAPI يرفض تشغيل SQL Server لو فيه Migrations ناقصة **بدل إنشاء الجداول تلقائيًا**.
- اختبار GitHub Actions مستقل ينشئ SQL Server 2022 Express مؤقتًا ببيانات **وهمية فقط**؛ يطبق Migration، يختبر HTTP Authentication و JWT وخصوصية الحسابات، ويُجري طلبَي إنشاء موعد متداخلين في اللحظة نفسها؛ ثم يُعيد تشغيل الـAPI ويتأكد من ثبات المرضى والعناوين والمواعيد.
- اختبارات CI لا تعني أن SQL Server خارجيًا دائمًا أصبح متوفرًا بالفعل، ولا أن قاعدة Render Free أصبحت دائمة.

## نشر قاعدة SQL Server دائمة للمشروع
1. اختر خادم SQL Server مُدارًا أو VPS مخصصًا يدعم SQL Server، بقرص ثابت ونسخ احتياطي ومراقبة.
2. أنشئ قاعدة **جديدة وفارغة** مثل `DareCareStaging`؛ لا تطبق InitialSqlServer فوق قاعدة قديمة أنشأتها SQLite أو فوق قاعدة SQL قديمة تحتوي جداول بدون `__EFMigrationsHistory`.
3. راجع الـMigration والـSQL الناتج منها، واحتفظ بنسخة احتياطية قبل أي تحديث. يحتاج حساب الترحيل فقط صلاحيات إنشاء/تعديل المخطط؛ حساب تشغيل الـAPI يجب أن يكون محدود الصلاحيات قدر الإمكان.
4. من جهاز آمن متصل بقاعدة Staging الجديدة، اضبط متغير البيئة `ConnectionStrings__DefaultConnection` على connection string مشفرة: `Encrypt=True;TrustServerCertificate=False` وشهادة TLS قابلة للتحقق. لا تحفظ القيمة في GitHub.
5. من جذر المستودع:

   ```bash
   dotnet restore DareCare.slnx
   dotnet tool restore
   dotnet ef migrations script --idempotent --project src/DareCare.Infrastructure --startup-project src/DareCare.Api --output staging-migrations-review.sql
   # افحص الـSQL قبل التنفيذ ثم:
   dotnet ef database update --project src/DareCare.Infrastructure --startup-project src/DareCare.Api
   ```

6. انشر Web Service منفصلة على Render من `deployment/render-sqlserver-staging.yaml` (خطة مدفوعة + قرص دائم للروشتات) أو على VPS عبر `deployment/compose.staging.yml`. أدخل أسرار SQL/JWT/SMTP داخل إعدادات البيئة لدى المزود، وليس داخل الملفات. استخدم HTTPS وصلاحيات CORS الدقيقة.
7. تحقق أن `GET /health/ready` يرجع 200، ثم نفّذ اختبارات فريق Flutter والويب باستخدام حسابات **وهمية فقط**. احذف سر bootstrap للأدمن بعد إنشائه.
8. جهّز نسخًا احتياطية **لـSQL Server وملفات الروشتات معًا** واختبر الاسترجاع الدوري. تخزين SQL الدائم وحده لا يحفظ الملفات.

## تحذيرات مهمة
- GitHub Actions SQL Server هنا مؤقت ولا يُستخدم لنشر بيانات، أو حفظها بعد انتهاء الاختبارات.
- Render Free مع SQLite كما هو، بياناته مؤقتة؛ لن تتحول لدائمة بمجرد دمج الكود.
- لا تجرِ `dotnet ef database update` تلقائيًا عند تشغيل كل instance. طبّق migrations بموافقة ومراجعة مسبقة.
- لا ترفع صور روشتات حقيقية أو سجلات صحية قبل إتمام المراجعة الأمنية والخصوصية والامتثال القانوني.
- اختبار CI يستخدم شهادة self-signed مع `TrustServerCertificate=True` **داخل بيئة الاختبار المحلية المؤقتة فقط**، وليس على قاعدة Staging/Production.
