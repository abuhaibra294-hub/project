import time
import requests

# ====== إعدادات أساسية ======
BOT_TOKEN      = "ضع_توكن_البوت_هنا"  # <-- حط التوكن اللي من BotFather
TELEGRAM_API   = f"https://api.telegram.org/bot{8254519170:AAG4R3gDFquwYHANhH0Ftbie3-ru2gy36a0}"

GROUP_CHAT_ID  = -1003156010894       # <-- chat_id بتاع مجموعة مهند

API_URL_BASE   = "https://YOUR_DOMAIN/get_queue_status.php"  # <-- غيّر YOUR_DOMAIN
API_SECRET     = "M7mod_Secret_2025_XYZ"                      # <-- نفس الـ secret في PHP
SESSION_ID     = 17                                          # <-- رقم الحصة دي

# ====== دوال تيليجرام بسيطة ======
def send_message(chat_id, text):
    url = f"{TELEGRAM_API}/sendMessage"
    data = {
        "chat_id": chat_id,
        "text": text,
        "parse_mode": "HTML"
    }
    try:
        r = requests.post(url, data=data, timeout=10)
        print("send_message response:", r.text)
    except Exception as e:
        print("Error sending message:", e)

def get_updates(offset=None, timeout=20):
    url = f"{TELEGRAM_API}/getUpdates"
    params = {
        "timeout": timeout,
    }
    if offset is not None:
        params["offset"] = offset
    try:
        r = requests.get(url, params=params, timeout=timeout+5)
        return r.json()
    except Exception as e:
        print("Error get_updates:", e)
        return None

# ====== دالة تجيب حالة الدور من الموقع ======
def fetch_queue_status():
    try:
        params = {
            "session_id": SESSION_ID,
            "secret": API_SECRET
        }
        r = requests.get(API_URL_BASE, params=params, timeout=10)
        print("API response:", r.text)
        return r.json()
    except Exception as e:
        print("Error fetching queue status:", e)
        return None

# ====== تجهيز رسالة الدور ======
def build_queue_message(data):
    if not data:
        return "⚠️ تعذر جلب بيانات الدور من الموقع."

    if "error" in data:
        return f"⚠️ خطأ من السيرفر: {data['error']}"

    session = data.get("session", {})
    bookings = data.get("bookings", [])
    current_index = data.get("current_index", None)

    if not bookings:
        return "📋 لا يوجد أي حجز في هذه الحصة حتى الآن."

    lines = []
    group_name   = session.get("group_name", "غير معروف")
    session_date = session.get("session_date", "")
    start_time   = session.get("start_time", "")

    lines.append(f"📚 <b>المجموعة:</b> {group_name}")
    if session_date:
        lines.append(f"📅 التاريخ: {session_date}")
    if start_time:
        lines.append(f"🕓 وقت الحصة: {start_time}")
    lines.append("-------------------------")

    if current_index is None:
        lines.append("🎉 لا يوجد دور حاليًا (لا حد جوه الحصة ولا حد مستني).")
        return "\n".join(lines)

    current = bookings[current_index]
    prev    = bookings[current_index - 1] if current_index - 1 >= 0 else None
    next1   = bookings[current_index + 1] if current_index + 1 < len(bookings) else None
    next2   = bookings[current_index + 2] if current_index + 2 < len(bookings) else None

    # الحالي
    lines.append("⏳ <b>الدور الحالي:</b>")
    lines.append(f"➡️ رقم {current['queue_order']} — {current['student_name']}")

    # اللي قبله
    if prev:
        lines.append("")
        lines.append("⬅️ <b>اللي قبله مباشرة:</b>")
        lines.append(f"رقم {prev['queue_order']} — {prev['student_name']}")

    # اللي بعده
    if next1:
        lines.append("")
        lines.append("🔜 <b>اللي بعده:</b>")
        lines.append(f"رقم {next1['queue_order']} — {next1['student_name']}")
    if next2:
        lines.append(f"ثم رقم {next2['queue_order']} — {next2['student_name']}")

    return "\n".join(lines)

# ====== اللوب الأساسي للبوت (Long Polling) ======
def main():
    print("Bot started...")
    last_update_id = None

    while True:
        updates = get_updates(offset=last_update_id, timeout=20)
        if not updates or not updates.get("ok"):
            time.sleep(3)
            continue

        for update in updates.get("result", []):
            update_id = update["update_id"]
            if last_update_id is None or update_id >= last_update_id:
                last_update_id = update_id + 1

            message = update.get("message") or update.get("edited_message")
            if not message:
                continue

            chat = message.get("chat", {})
            chat_id = chat.get("id")
            text = message.get("text", "")

            # نشتغل بس لو الرسالة من جروب مهند
            if chat_id != GROUP_CHAT_ID:
                continue

            # أمر /queue يطلع حالة الدور
            if text.startswith("/queue"):
                data = fetch_queue_status()
                msg  = build_queue_message(data)
                send_message(GROUP_CHAT_ID, msg)

        time.sleep(1)


if __name__ == "__main__":
    main()
