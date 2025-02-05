### 📌 **RankUtils Plugin**

---

#### 🔥 **Описание**
Плагин `RankUtils` автоматически **обнуляет ранги** (`rank` и `exp`) забаненных игроков в системе **Ranks API**.  
Также в плагине есть **команды для ручного сброса рангов и статистики** игроков, а также отдельная команда для **очистки рангов всех забаненных игроков**.

Плагин поддерживает **кэширование рангов перед баном** и **автоматическое восстановление после разбана**, если время хранения кэша не истекло.

---

### ⚙ **Функционал**
✅ **Автоматическое обнуление рангов** при бане игрока  
✅ **Команды для очистки рангов и статистики игроков**  
✅ **Гибкие настройки сброса данных**  
✅ **Очистка рангов всех забаненных игроков**  
✅ **Кэширование рангов перед баном и автоматическое восстановление после разбана**  
✅ **Поддержка CRON-задач для автоматического сброса рангов по расписанию, в можно настроить кастомные команды по расписанию**

---

### 📥 **Установка**
1. Скачайте `.zip` плагина и поместите его содержимое в папку **`/csgo/addons/counterstrikesharp/plugins/`**
2. Убедитесь, что **Ranks API** установлен и работает
3. Убедитесь, что **IksAdmin API** установлен и работает
4. Перезапустите сервер

---

### 🛠 **Использование**

#### **🔄 Автоматическое обнуление**
Плагин **автоматически** очищает ранги забаненных игроков.  
Действует на **все баны**, независимо от причины.

💾 **Кэширование перед удалением**:
- При бане **ранг игрока сохраняется в кэш** (`rank_cache.json`).
- Если игрок **разбанен до истечения времени кэша**, его ранг и статистика **автоматически восстанавливаются**.

⌛ **Время хранения кэша** настраивается в `config.json` (параметр `CacheSaveBanRank`).

---

### **⚙ Команды для ручной очистки рангов и статистики**

#### 🔄 **Сброс рангов и статистики вручную**
Используйте команду:

```sh
css_lr_reset_ranks <all|exp|stats|play_time>
```

📌 **Команда работает только в консоли сервера!**

#### **📝 Доступные аргументы команды**
- **`all`** – **полный сброс** всех данных.
- **`exp`** – сброс **очков опыта** (`value`, `rank`).
- **`stats`** – сброс **статистики** (`kills`, `deaths`, `shoots`, `hits`, `headshots`, `assists`, `round_win`, `round_lose`).
- **`play_time`** – сброс **времени игры**.

**Пример использования:**
```sh
css_lr_reset_ranks exp  # Сбросит только очки опыта
css_lr_reset_ranks all  # Полный сброс данных
```

---

### **♻ Очистка рангов всех забаненных игроков**
Эта команда **сбрасывает ранги** (`rank` и `exp`) **только у игроков, которые были забанены**:

```sh
css_lr_clear_rank_if_banned
```

📌 **Команда работает только в консоли сервера!**  
📌 **Удаляет только ранги забаненных игроков, не затрагивая остальных.**

---

### **⏲ Настройка автоматического сброса по CRON**
Теперь можно настроить **автоматический сброс рангов по расписанию** с помощью CRON.  
Файл конфигурации: **`config.json`**

#### **📄 Пример настройки CRON в `config.json`**
```json
"CronSettings": [
    {
        "CronExpression": "* * * * *",
        "Command": "css_lr_reset_ranks exp"
    },
    {
        "CronExpression": "0 0 * * *",
        "Command": "css_lr_clear_rank_if_banned"
    }
]
```

📌 **Как работает CRON:**
| CRON               | Расписание                        |
|--------------------|--------------------------------|
| `* * * * *`       | **Каждую минуту**              |
| `*/5 * * * *`     | **Каждые 5 минут**             |
| `0 * * * *`       | **Каждый час**                 |
| `0 0 * * *`       | **Раз в сутки (в полночь)**    |

---

### ⚠ **Требования**
- **CounterStrikeSharp API (>=v305) [https://github.com/roflmuffin/CounterStrikeSharp]**
- **Ranks API [https://github.com/partiusfabaa/cs2-ranks]**
- **IksAdmin API [https://github.com/Iksix/Iks_Admin]**