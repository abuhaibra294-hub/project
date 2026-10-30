# DareCare Sprint 9 — ملفات الروشتات وسلامة النشر

## التعديلات

1. **حفظ الملفات الخاصة**: لا يكتب الـBackend أكثر من 8 MiB، ويتحقق من البصمة والامتداد والحجم الفعلي، ويحذف الملفات الجزئية عند خطأ القراءة أو الإلغاء، حتى إذا كان الملف قد وصل فعلًا لطول `IFormFile.Length` قبل وقوع الاستثناء. الملفات الناجحة فقط تحصل على مفاتيح تخزين معتمة.
2. **الـSQL Server الصريحة**: لو `Database__Provider=SqlServer` ولكن `ConnectionStrings__DefaultConnection` ناقصة، تتوقف الخدمة بخطأ إعدادات، ولا تتحول إلى SQLite. الـRender Free التجريبية المضبوطة عمدًا على SQLite تستمر كما هي.
3. **النسخة المنشورة**: `GET /health` يعرض `deployedCommit` من متغير Render الموثق `RENDER_GIT_COMMIT` إن كان SHA صالحًا، ولا يعرض أسرارًا أو كلمات مرور. النتيجة `null` في الأجهزة التي لا تضبط المتغير.
4. **فحص قراءة فقط**: يمكن مقارنة إصدار GitHub بالإصدار الذي يعمل فعلًا على Render عبر `scripts/verify_render_release.py`، بدون حسابات تجريبية جديدة أو تعديل بيانات.

## إثبات إصدار Render الحالي

من مجلد المستودع على جهازك بعد جلب آخر تغييرات فرع `darecare-demo-free`:

```bash
git fetch origin darecare-demo-free
python3 scripts/verify_render_release.py --base-url https://project-kv53.onrender.com --expected-commit "$(git rev-parse origin/darecare-demo-free)"
```

- `PASS`: نفس الـCommit يعمل فعلًا وقاعدة البيانات جاهزة.
- `STALE`: الـCommit الجاري تشغيله أقدم من GitHub؛ افحص Render Dashboard > Events / Deploys وحالة GitHub Integration وAuto-Deploy.
- `no deployedCommit`: غالبًا Render ما زال يشغّل إصدارًا سابقًا لا يدعم إظهار النسخة؛ هذا ليس إثباتًا لفشل قاعدة البيانات.
- `/health/ready` قد يحتاج وقتًا عند استيقاظ الخدمة المجانية؛ كرر الفحص إذا كانت نائمة.
- **لا تفعّل النشر أو إعادة التشغيل بشكل عشوائي**: قاعدة SQLite المجانية وملفات التجربة مؤقتة، وإعادة النشر قد تمحوها. احتفظ فقط ببيانات وهمية.
- الـScript يدعم HTTPS لخدمات `*.onrender.com` فقط، أو HTTP المحلي داخل اختبارات Docker؛ لا يرسل كلمات مرور أو JWT ولا يستدعي عمليات كتابة.

## حدود مهمة

- هذا التحسين **ليس فحص برمجيات خبيثة** أو إثبات سلامة طبية للملفات. ما زال يلزم فحص آمن ومعالجة ملفات وتقارير خصوصية قبل بيانات مرضى حقيقية.
- قاعدة SQL Server دائمة لم تُنشأ على Render ضمن Sprint 9؛ Migration واختبارات SQL Server موجودة من Sprint 8 وتحتاج خادمًا دائمًا ونسخًا احتياطية.
- مقارنة إصدار الـCommit لا تضمن أن كل التكاملات أو SMTP أو النسخ الاحتياطي سليمة؛ هي فحص نشر وجاهزية أساسي.
