# DareCare — حزمة التسليم لـ Flutter وFrontend (Sprint 6)

> **الحالة الصادقة:** هذه حزمة التكامل مشتقة من الكود الفعلي. اجتاز استخراج المسارات والفحص الثابت؛ **لم يُشغَّل Build أو HTTP E2E في بيئة إعداد الحزمة** لعدم توفر .NET SDK. يجب تشغيل ملفات التحقق المحلية قبل استخدام أي رابط Staging.

## 1. ما الذي يستلمه كل مطور؟

- `integration/DareCare_Postman_Collection.json`: طلبات كل الـ93 Endpoint، مصنفة إلى Public/Authentication/Patient/Nurse/Pharmacy/Admin/Shared؛ بها أربع Requests جاهزة لتسجيل الدخول وتخزين JWT/refresh في البيئة المحلية.
- `integration/DareCare_Local.postman_environment.json`: `baseUrl=http://127.0.0.1:5208` مع كل المتغيرات فارغة، **بدون أي أسرار**. للاستضافة بدّل baseUrl إلى **HTTPS الحقيقي بعد نشر الخدمة**.
- `integration/DareCare_OpenAPI_SourceSnapshot.json`: وصف OpenAPI أولي مشتق من الـC#، **ليس صادرًا من ASP.NET وقت التشغيل**. المصدر الرسمي بعد التشغيل: `GET /openapi/v1.json`.
- `integration/DareCare_API_Endpoints.json`: الفهرس الحقيقي لـ93 Controller API endpoints والأدوار.
- `DareCare.http`: أمثلة API داخل Visual Studio / VS Code REST Client.
- `integration/client-examples/darecare_flutter_api.dart`: مثال HTTP مع تسجيل دخول وRefresh مرة واحدة، يحتاج Adapter تخزين آمن في Flutter.
- `integration/client-examples/darecare_web_client.mjs`: مثال Web ES module يحفظ JWT في الذاكرة فقط ولا يستخدم localStorage (للتجربة).
- `docs/TEAM_HANDOFF_AR.md`: هذا الدليل. `docs/STAGING_DEPLOY_AR.md`: إعداد HTTPS وStaging؛ و`docs/RENDER_DEPLOY_AR.md` و`render.yaml`: خيار Render البديل لـRailway.

**ملاحظة:** Swift/Flutter Mobile لا يخضع لـBrowser CORS. أما ويب الممرض/الصيدلية/الإدارة فيحتاج Origin مضبوط على API. لا تتعامل مع مرور الطلبات من Postman على أنه دليل على ضبط CORS.

## 2. خطوات التشغيل والتسليم (بالترتيب)

1. **Backend**: تشغيل `START_DARECARE.cmd` وانتظار نجاح `dotnet build` والاختبارات و`/health/ready`.
2. تشغيل `VERIFY_TEAM_API.cmd` لفحص Health/401/Discovery/CORS؛ ثم `SMOKE_DARECARE.cmd` لرحلة وهمية كاملة.
3. تشغيل `PREPARE_TEAM_DEMO.cmd` لإنشاء حساب عميل وممرض وصيدلية وهميين مع اعتمادهم وإتمام رحلة طلب؛ تُحفظ بيانات الدخول داخل `.localdata/demo-accounts.txt` على **جهازك فقط**؛ حساب الإدارة منفصل في `.localdata/admin-login.txt`.
4. تشغيل `EXPORT_LIVE_OPENAPI.cmd`؛ يحفظ ملف Runtime حقيقي في `.localdata/DareCare_OpenAPI_LIVE.json` ويتأكد أن **كل مسارات الـ93 موجودة**. إذا فشل المقارنة فلا تسلّم للفرونت.
5. استيراد Postman Collection + Environment، وإدخال بيانات الحسابات المحلية في Environment على جهاز كل مطور، وليس داخل ملفات JSON المرفوعة لـ GitHub.
6. بعد نشر Staging على HTTPS فقط (مثل Render حسب docs/RENDER_DEPLOY_AR.md)، أنشئ Environment أخرى بالفروع نفسها مع `baseUrl=https://YOUR_DOMAIN` وبيانات حسابات **Staging** التجريبية، ثم سلّم الرابط للفريق.

## 3. تقسيم المسارات حسب الفريق

| التطبيق | أهم المجموعات | نوع المصادقة |
|---|---|---|
| Flutter (العميل) | auth، patients/me، addresses، favorites، nurses/search، services/search، matching، bookings، reviews، prescriptions، orders/mine، notifications، support | Patient JWT |
| Web 1 (الممرض) | auth، nurses/me، nurses/me/skills، nurses/me/slots، bookings/mine، accept/reject/start/complete، dashboard، notifications | Nurse JWT |
| Web 2 (الصيدلية) | auth، pharmacies/me، prescriptions/inbox، orders/inbox، quote/prepare/dispatch/deliver، dashboard، support | Pharmacy JWT |
| Web 2 (الإدارة) | auth، admin/nurses/pending، admin/pharmacies، admin/users، admin/audit، dashboard، support/admin/tickets | Admin JWT |

الـFrontend لا يحتاج الاتصال المباشر بـ SQL Server؛ يرسل HTTPS/JSON (أو `multipart/form-data` لرفع الروشتة).

## 4. الـAuth مع Postman وواجهات التطبيق

`POST /api/v1/auth/login`:
```json
{"email":"patient-demo@example.test","password":"STRONG_TEST_PASSWORD"}
```
رد النجاح من `AuthResponse`:
```json
{
  "userId":"00000000-0000-0000-0000-000000000001",
  "accessToken":"JWT_ACCESS_TOKEN_PLACEHOLDER",
  "expiresAtUtc":"2026-10-23T21:00:00Z",
  "roles":["Patient"],
  "refreshToken":"REFRESH_TOKEN_PLACEHOLDER",
  "refreshTokenExpiresAtUtc":"2026-11-06T21:00:00Z"
}
```
الأرقام والتوكنات **أمثلة توضيحية فقط**، وليست Credentials حقيقية. تسجيل الدخول يختلف بالـRole، لكن يستخدم نفس الـEndpoint؛ Postman يوفر طلبات Login مستقلة تخزن `patientToken` و`nurseToken` و`pharmacyToken` و`adminToken`.

- `Authorization: Bearer <accessToken>` على كل API محمية؛ Access Token عمره الافتراضي 15 دقيقة.
- `POST /api/v1/auth/refresh` مع `{"refreshToken":"..."}` لتجديد الجلسة (Rotation).
- `POST /api/v1/auth/logout` يلغي جلسة Refresh المحددة؛ `logout-all` يلغي الجلسات والأكواد الحالية.
- رد `401`: غير مسجل الدخول أو انتهت صلاحية JWT. رد `403`: الدور/ملكية الطلب ممنوع. عند 401 جرّب refresh **مرة واحدة فقط** ثم اطلب إعادة تسجيل الدخول إن فشل.
- Flutter يخزن التوكنات عبر تخزين آمن خاص بالمنصة؛ لا تضعها في الـSource/Logs. وبالنسبة لتطبيقات الويب، خطط لتخزين آمن يقلّل مخاطر XSS، واستبدال refresh في JSON بحل HttpOnly Cookie/BFF عند الانتقال للإنتاج.

## 5. JSON / أخطاء / Pagination / Enums — عقد العمل المقترح

### الـPagination المطبق بالفعل
```json
{"page":1,"pageSize":20,"total":0,"items":[]}
```
`pageSize` حتى 50 في مسارات البحث الجديدة. الخطأ 400 عند قيم غير مسموحة؛ الأفضل عرض رسالة مناسبة من `message` أو `detail` أو `errors`.

### الأخطاء — الواقع الحالي
بعض endpoints ترجع `{ "message": "..." }` يدويًا؛ Validation Errors التي يصدرها `[ApiController]` تأتي غالبًا بصيغة RFC 7807 مع `errors`، والأخطاء غير المعالجة في Staging/Production بصيغة ProblemDetails عامة. لذلك **لا تفترض شكل خطأ واحد موحّد في Sprint 6**، واستخدم ترتيب العرض:
`errors` → `detail` → `message` → `title` → رسالة عامة محلية.
اتفقوا على توحيد أخطاء v2 بعد نجاح التكامل، بدل تعديل العقود الموجودة وسط تنفيذ الفريق.

### حالات الطلبات بأسمائها بالضبط (String enums)
- `BookingStatus`: `Pending`, `Accepted`, `InProgress`, `Completed`, `Cancelled`, `Rejected`.
- `PharmacyOrderStatus`: `Quoted`, `Confirmed`, `Preparing`, `OutForDelivery`, `Delivered`, `Declined`.
- `SupportTicketStatus`: `Open`, `InProgress`, `Resolved`, `Closed`.
- `SupportTicketCategory`: `Account`, `Booking`, `Pharmacy`, `Other`.

### أهم الطلبات
إنشاء حجز بعنوان محفوظ:
```json
{"nurseProfileId":"NURSE_GUID","availabilitySlotId":"SLOT_GUID","careServiceId":"SERVICE_GUID","addressId":"ADDRESS_GUID"}
```
أو إرسال `serviceAddress` بدل `addressId`؛ **لا ترسل الاثنين معًا**.

رفع روشتة: `POST /api/v1/prescriptions` عبر Multipart: `pharmacyProfileId`, `bookingId` (اختياري), `notes` (اختياري), `file` (PDF/PNG/JPG حتى 8MB). يجب تحديد ملف محلي في Postman يدويًا؛ ولا يمكن اختباره بـ JSON.

## 6. اختبار الصلاحيات المطلوب قبل إعلان الجاهزية

- عميل A يفتح `/api/v1/auth/me`: `200`، من غير JWT: `401`.
- عميل A يفتح `/api/v1/admin/overview`: `403`.
- عميل B يحاول عرض تذكرة دعم/تقييم تخص A: يجب أن يرفضه السيرفر `403` أو لا يجد المورد حسب تصميم endpoint، لا مجرد إخفاء الزر بالواجهة.
- ممرض غير معتمد لا يظهر في `/api/v1/nurses/search`.
- تنفيذ booking ومعالجة الروشتة ببيانات وهمية عبر `SMOKE_DARECARE.cmd`.
- قبل ربط الـFrontend يجب نجاح المسارات في الـRuntime OpenAPI وملف QA، وليس نجاح قراءة ملفات JSON فقط.

## 7. بيانات الدخول وعنوان الخدمة

**مهم:** لا توجد URL منشورة من طرفنا ضمن الحزمة؛ `http://127.0.0.1:5208` يعمل على جهاز الـBackend فقط، وليس رابطًا يستخدمه مطور آخر عبر الإنترنت. الاستضافة تحتاج VPS/Cloud وSQL Server وDNS وSMTP واتصال فعلي؛ راجع `docs/STAGING_DEPLOY_AR.md`.

بعد إنشاء الرابط، شارك معهم فقط URL + ملف Postman **بدون أسرار**. سلّم بيانات دخول كل دور تجريبي إلى صاحبه عبر قناة خاصة منفصلة. لا تشارك كلمة سر الإدارة أو أي بيانات مرضى حقيقية.
