# DareCare — ابدأ ربط Flutter والويب هنا
**تاريخ مراجعة المصدر: 16 نوفمبر 2026 — هذه وثيقة تسليم تطويري، وليست شهادة جاهزية إنتاجية.**

> **اختيار الفرع مهم:** مشروع DareCare موجود في مستودع `abuhaibra294-hub/project` على فرع `darecare-demo-free`. فرع `main` ليس الفرع الأساسي لتطبيق DareCare. لا تدمج تغييرات DareCare في `main` لمجرد أنه الفرع الافتراضي.

## 1. القرار العملي: يبدأ فريق الواجهات الآن، لكن على بيئة مؤكدة

يمكن تنفيذ واجهات Flutter وWeb وربطها بواجهات الـAPI الحالية. تحتوي الحزمة على **93 endpoint** موزعة حسب الأدوار `Patient`, `Nurse`, `Pharmacy`, `Admin`، مع الأمثلة والعقود الجاهزة.

**المتاح في المستودع:**
- [دليل العقود وتوزيع الفريق](TEAM_HANDOFF_AR.md).
- [Postman Collection](../integration/DareCare_Postman_Collection.json) و[بيئة Postman محلية بدون أسرار](../integration/DareCare_Local.postman_environment.json).
- [فهرس المسارات والأدوار](../integration/DareCare_API_Endpoints.json).
- [مثال Flutter](../integration/client-examples/darecare_flutter_api.dart) و[مثال Web JavaScript](../integration/client-examples/darecare_web_client.mjs).
- [لقطة OpenAPI مستخرجة من المصدر](../integration/DareCare_OpenAPI_SourceSnapshot.json) **للمراجعة فقط**؛ المرجع النهائي هو `GET /openapi/v1.json` من الخدمة الفعلية وقت التشغيل.
- [قواعد الـAPI في Postman/VS Code](../DareCare.http).

## 2. قبل بدء الاتصال بالسيرفر

1. اختبر `GET /health` و`GET /health/ready` على **نفس عنوان API** المراد تسليمه. يجب أن يعيد `/health/ready` كود HTTP 200 مع `status=ready`.
2. استخرج `GET /openapi/v1.json` من السيرفر الحي، وقارن المسارات الـ93 بالـmanifest، بدل الاعتماد فقط على JSON ثابت في GitHub. استخدم `VERIFY_TEAM_API.cmd` و`EXPORT_LIVE_OPENAPI.cmd` عند تشغيل المشروع محليًا.
3. نفّذ رحلة اختبار ببيانات وهمية: Patient register/login → اكتشاف ممرض/خدمة → حجز → Nurse accept/start/complete؛ ثم مسار Prescription → Pharmacy quote → Patient confirm → Pharmacy prepare/dispatch/deliver.
4. تأكد من `401` عند عدم تسجيل الدخول، و`403` عند استخدام دور غير مسموح، وعدم إمكانية حساب آخر قراءة بيانات المريض.
5. لا ترسل Refresh Tokens أو كلمات المرور عبر Git، Postman Collection مشترك، screenshots، أو سجلات متاحة للجميع.

**عنوان التطوير المحلي في Postman:** `http://127.0.0.1:5208`. هذا عنوان جهاز مطور الـBackend فقط؛ لن يعمل تلقائيًا على أجهزة الفريق الأخرى. يشترط في اتصال الإنترنت HTTPS.

**عنوان Render المذكور في ملاحظات المشروع:** `https://project-kv53.onrender.com` — **غير مثبت الجاهزية بتاريخ هذه الوثيقة**؛ لا تسلمه كخدمة مؤكدة قبل اختبار `/health/ready` وOpenAPI والتكامل الكامل. Render Free يحتفظ ببيانات SQLite مؤقتة فقط؛ إعادة التشغيل أو النشر قد تفقد الحسابات والملفات التجريبية.

## 3. خريطة الربط حسب العضو

| الواجهة | الوظائف الأولى التي تُربط | الدور |
|---|---|---|
| Flutter / المريض | `/api/v1/auth/register/patient`, `auth/login`, `auth/me`, `patients/me`, `nurses/search`, `services/search`, `bookings`, `bookings/mine`, `prescriptions`, `orders/mine`, `notifications` | Patient |
| Web / الممرض | `auth/login`, `nurses/me`, `nurses/me/slots`, `bookings/mine`, `bookings/{id}/accept`, `start`, `complete`, `notifications`, `dashboard/me` | Nurse |
| Web / الصيدلية | `auth/login`, `pharmacies/me`, `prescriptions/inbox`, `orders/inbox`, `orders/prescriptions/{id}/quote`, `orders/{id}/prepare`, `dispatch`, `deliver` | Pharmacy |
| Web / الإدارة | `auth/login`, `admin/overview`, `admin/nurses/pending`, `admin/pharmacies`, `admin/users`, `support/admin/tickets` | Admin |

كل المسارات في الجدول نسبية إلى `/api/v1/` حين لا يظهر البادئة. **الـFrontend لا يتصل بقاعدة SQL Server مباشرة.**

## 4. المصادقة والأخطاء

- `POST /api/v1/auth/login` مع `{ "email": "...", "password": "..." }` يرجع `accessToken`, `refreshToken`, `roles`, ومدد الانتهاء.
- أرسل `Authorization: Bearer <accessToken>` مع المسارات المحمية.
- عند `401` جرّب `POST /api/v1/auth/refresh` **مرة واحدة**، ثم أعد طلب المستخدم مرة واحدة فقط. عند `403` لا تعِد تسجيل الدخول تلقائيًا؛ المستخدم لا يملك الصلاحية.
- Flutter يستعمل تخزينًا آمنًا للرموز عبر Adapter يضيفه مطور التطبيق. مثال Web يحفظ JWT في الذاكرة للتطوير فقط، **ولا يعتبر تصميم جلسات إنتاجيًا**.
- Pagination في مسارات البحث: `{ "page": 1, "pageSize": 20, "total": 0, "items": [] }`.
- عرض أخطاء API حسب الترتيب `errors` ثم `detail` ثم `message` ثم `title`، ولا تفترض أن كل endpoint يرجع خطأ بالشكل نفسه.
- حالات Booking: `Pending`, `Accepted`, `InProgress`, `Completed`, `Cancelled`, `Rejected`.

**للويب تحديدًا:** نجاح Postman لا يثبت نجاح CORS في المتصفح. السيرفر لا يقبل Origins عشوائية؛ لا بد من إضافة عنوان HTTPS الفعلي للواجهة إلى `Cors:AllowedOrigins` بإعدادات الـBackend. لا تستخدم `*`، ولا تغيّر السيرفر إلى Development على الإنترنت. يظل Flutter mobile غير خاضع لـBrowser CORS.

## 5. ما تم التحقق منه، وما لم يُتحقق منه

**نتائج مثبتة على commit `90067321f1f95cfc502f8b470127743df74fe5bb` من فرع Sprint 9:**
- [CI بناء .NET واختبار API ومشغّل Render Docker: ناجح](https://github.com/abuhaibra294-hub/project/actions/runs/41111309898).
- [CI SQL Server migrations وHTTP/auth/restart: ناجح](https://github.com/abuhaibra294-hub/project/actions/runs/41111309966).
- [Sprint 9 PR #3](https://github.com/abuhaibra294-hub/project/pull/3) **لا يزال Draft وغير مدموج** في `darecare-demo-free` وقت المراجعة. نجاح الـCI لهذا الفرع لا يعني أن Render يشغّله.
- **لم تُثبت** استجابة Render العامة، أو SHA المنشور، أو CORS الفعلي، أو توفر حسابات تجريبية صالحة على الـStaging. لا تسجل هذه البنود كناجحة حتى تُختبر مباشرة.

## 6. معيار تسليم 'الربط شغال' بدلاً من 'الكود موجود'

- [ ] إثبات HTTP 200 من `/health/ready` على URL المقصود.
- [ ] حفظ Live OpenAPI من الخادم نفسه وتطابقه مع عقد الـ93 endpoint.
- [ ] نجاح تسجيل الدخول والـrefresh من Flutter/Web مع بيانات وهمية فقط.
- [ ] نجاح CORS من Origin الويب الحقيقي (browser preflight + request).
- [ ] نجاح رحلة المريض والممرض والصيدلية HTTP end-to-end على نفس السيرفر.
- [ ] حسابات اختبار منفصلة لكل دور وإرسال بياناتها لقنوات خاصة منفصلة.
- [ ] عدم مشاركة أسرار الإدارة أو أي روشتات/بيانات مرضى حقيقية.

**تنبيه:** هذا MVP لمشروع تخرج، وليس نظامًا طبيًا إنتاجيًا. الاستضافة الدائمة، SMTP الحقيقي، نسخ احتياطية، فحص الملفات والخصوصية، مراجعات الأمان والامتثال متطلبات منفصلة قبل استقبال أي بيانات طبية حقيقية.
