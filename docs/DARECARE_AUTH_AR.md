# تسجيل الدخول وأمان الحساب في DareCare

- **كود البريد OTP** من 6 أرقام لتأكيد البريد واستعادة كلمة المرور. صلاحيته 10 دقائق، ويُقبل مرة واحدة. إعادة الإرسال متاحة بعد دقيقة، ولكل كود خمس محاولات. إرسال البريد يستخدم SMTP الموجود، وبيئة Development تستخدم صندوق الاختبار المحلي عند غياب SMTP.
- **تطبيق Authenticator** اختياري للحسابات. من «أمان الحساب» يضيف المستخدم المفتاح يدويًا في التطبيق، يكتب كود التفعيل ويحفظ أكواد الاسترداد. كل كود استرداد صالح لمرة واحدة. تشغيل التحقق أو إيقافه يستبدل جلسة الجهاز الحالي ويلغي الجلسات السابقة.
- **الدخول بجوجل** لحسابات المرضى. الدخول لأول مرة ينشئ حساب مريض وملف مريض عادي. صاحب الحساب الموجود يؤكد بريده، ويسجل الدخول بكلمة المرور، ويربط حساب جوجل المطابق من «أمان الحساب». طريقة الربط نفسها لا تُمنح لحسابات الإدارة والتمريض والصيدليات.

## الإعداد

1. من Google Cloud أنشئ OAuth Client ID من نوع Web application، وأضف عنوان واجهة الويب إلى **Authorized JavaScript origins**. لنسخة DareCare Lite على Render استخدم أصل الخدمة نفسه، مثل `https://project-kv53.onrender.com`. للإعداد المحلي استخدم أصل التطوير الفعلي، مثل `http://localhost:5208`.
2. اضبط `Auth__Google__WebClientId` للويب. اضبط `Auth__Google__FlutterServerClientId` لو كان معرّف الـserver client المستخدم في Flutter مختلفًا. على Flutter سجّل إعدادات Android/iOS في مشروع Google نفسه واستخدم قيمة الـserver client ID عند طلب Google ID token. الـAPI يحتاج معرّف العميل العام فقط لهذا المسار.
3. اضبط `Smtp__Host` و`Smtp__FromEmail` وإعدادات الاعتماد في بيئة النشر. تفعيل `Auth__RequireConfirmedEmail=true` يجعل تأكيد البريد شرطًا للدخول بكلمة المرور. نسخة Render المجانية تضبطه على `false` افتراضيًا.
4. في بيئة تستخدم قاعدة بيانات دائمة، اضبط `Auth__DataProtectionKeyDirectory` على مسار دائم ومشترك بين نسخ التطبيق. انقل حلقة مفاتيح ASP.NET Core Data Protection الحالية إلى المسار لو كانت البيئة تعمل بالفعل، وقيّد صلاحيات المجلد. بها تُشفّر مفاتيح Authenticator. خزّن `Jwt__Key` في إعدادات الخادم أيضًا. الإضافة تستخدم جداول ASP.NET Core Identity الموجودة وتعمل دون ترحيل لقاعدة البيانات.

## عقد الويب وFlutter

كل المسارات تحت `/api/v1/auth`. الطلبات التالية `POST` إلا حيث مذكور.

| المسار | جسم الطلب أو النتيجة |
| --- | --- |
| `/register/patient` و`/register/nurse` | أضف `useEmailOtp: true` إلى طلب التسجيل لإرسال كود رقمي. |
| `/resend-verification` و`/forgot-password` | `{ "email": "...", "useEmailOtp": true }`. استجابة `202` عامة حتى لو كان البريد غير مسجّل. |
| `/verify-email` | `{ "email": "...", "code": "123456" }`. |
| `/reset-password` | `{ "email": "...", "code": "123456", "newPassword": "..." }`. |
| `GET /google/config` | `{ "webClientId": "..." }`. القيمة `null` عند غياب إعداد الويب. |
| `/google` | `{ "idToken": "..." }`. إذا سبق تسجيل البريد في DareCare تظهر `409` مع `google_link_required`. |
| `/google/link` | جلسة مريض سارية مع `{ "idToken": "...", "currentPassword": "..." }`. النجاح `204`. |
| `/authenticator/setup` | جلسة سارية. يعيد `sharedKey` و`authenticatorUri` وموعد انتهاء الإعداد. |
| `/authenticator/enable` | جلسة سارية مع `{ "code": "123456" }`. استعمل `session` الجديد واعرض `recoveryCodes` للمستخدم مرة واحدة. |
| `/authenticator/disable` | جلسة سارية مع كود تطبيق جديد أو كود استرداد. الرد يحتوي جلسة بديلة. |
| `/authenticator/login` | `{ "challengeToken": "...", "code": "123456" }`. يقبل كذلك كود استرداد. |

بعد كلمة المرور أو Google ID token افحص الحالة. `200` يعطي جلسة DareCare. `202` يعطي `twoFactorRequired: true` و`challengeToken` صالحًا لخمس دقائق، وبعده اطلب كود التطبيق. احتفظ بالتحدي في الذاكرة حتى ينتهي التسجيل. عقد تأكيد البريد واستعادة كلمة المرور يقبل أيضًا الأكواد الطويلة التي تستخدمها الإصدارات الأقدم.

يتحقق الـAPI من Google ID token عند الخادم ويستخدم معرّف الحساب `sub` للربط. قبول التسجيل يقتصر على Gmail أو Google Workspace ببريد مؤكد. راجع [إعداد Google Identity Services](https://developers.google.com/identity/gsi/web/guides/get-google-api-clientid) و[التحقق من Google ID token](https://developers.google.com/identity/gsi/web/guides/verify-google-id-token).
