#!/usr/bin/env python3
"""
Генератор курсов DexterType.

Строит DexterType/Courses/ru-basic.json и en-basic.json из словарей в tools/courses/*.txt.
Курс устроен по принципу «ступени -> уроки -> упражнения»: на каждом уроке вводятся 1-4 новые клавиши,
а все упражнения урока используют ТОЛЬКО уже изученные символы (это проверяется при генерации).

Запуск:   python tools/generate_courses.py            # пересобрать JSON
          python tools/generate_courses.py --check    # только проверить существующие JSON
Результат детерминирован (фиксированный seed): повторный запуск даёт тот же курс.
"""
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "courses")
OUT = os.path.join(HERE, "..", "DexterType", "Courses")

# --- Раскладки (должны совпадать с Dexter.Core/Keyboard/KeyboardLayoutDef.cs) -------------------------------
F0 = [0, 0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7]
F1 = [0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7, 7, 7]
F2 = [0, 1, 2, 3, 3, 4, 4, 5, 6, 7, 7]
F3 = [0, 1, 2, 3, 3, 4, 4, 5, 6, 7]
FINGER_NAMES = ["левый мизинец", "левый безымянный палец", "левый средний палец", "левый указательный палец",
                "правый указательный палец", "правый средний палец", "правый безымянный палец", "правый мизинец"]

LAYOUT_ROWS = {
    "ru": [("ё1234567890-=", 'Ё!"№;%:?*()_+', F0), ("йцукенгшщзхъ\\", "ЙЦУКЕНГШЩЗХЪ/", F1),
           ("фывапролджэ", "ФЫВАПРОЛДЖЭ", F2), ("ячсмитьбю.", "ЯЧСМИТЬБЮ,", F3)],
    "en": [("`1234567890-=", "~!@#$%^&*()_+", F0), ("qwertyuiop[]\\", "QWERTYUIOP{}|", F1),
           ("asdfghjkl;'", 'ASDFGHJKL:"', F2), ("zxcvbnm,./", "ZXCVBNM<>?", F3)],
}


def layout_tables(lang):
    typeable, finger = {" "}, {" ": -1}
    for normal, shifted, fingers in LAYOUT_ROWS[lang]:
        for n, s, f in zip(normal, shifted, fingers):
            typeable.update((n, s))
            finger[n] = finger[s] = f
    return typeable, finger


# --- Данные -------------------------------------------------------------------------------------------------
def read_words(name):
    path = os.path.join(DATA, name)
    out = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("#"):
                continue
            out.extend(line.split())
    return out


def read_lines(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return [l.strip() for l in f if l.strip() and not l.startswith("#")]


# --- Языки --------------------------------------------------------------------------------------------------
LANGS = {
    "ru": dict(
        id="ru-basic", title="Русский: базовый курс", vowels="аеиоуыэюя",
        parts=[
            ("Домашний ряд", [("letters", "фыва"), ("letters", "олдж"), ("letters", "пр")]),
            ("Верхний ряд", [("letters", "ке"), ("letters", "нг"), ("letters", "уц"), ("letters", "шщ"),
                             ("letters", "йз"), ("letters", "хъэ")]),
            ("Нижний ряд", [("letters", "ми"), ("letters", "ть"), ("letters", "сч"), ("letters", "бю"),
                            ("letters", "я")]),
            ("Заглавные буквы", [("caps", "left"), ("caps", "right")]),
            ("Цифры", [("digits", "4567"), ("digits", "38"), ("digits", "29"), ("digits", "10")]),
            ("Знаки препинания", [("punct", ".,"), ("punct", '!?:;'), ("punct", '-()"')]),
            ("Тексты", [("text", 1), ("text", 2), ("text", 3)]),
        ],
        home="ФЫВА ОЛДЖ",
    ),
    "en": dict(
        id="en-basic", title="English: базовый курс", vowels="aeiou",
        parts=[
            ("Домашний ряд", [("letters", "asdf"), ("letters", "jkl"), ("letters", "gh")]),
            ("Верхний ряд", [("letters", "ei"), ("letters", "ru"), ("letters", "ty"), ("letters", "wo"),
                             ("letters", "qp")]),
            ("Нижний ряд", [("letters", "cm"), ("letters", "vb"), ("letters", "nx"), ("letters", "z")]),
            ("Заглавные буквы", [("caps", "left"), ("caps", "right")]),
            ("Цифры", [("digits", "4567"), ("digits", "38"), ("digits", "29"), ("digits", "10")]),
            ("Знаки препинания", [("punct", ".,'"), ("punct", '!?:;'), ("punct", '-()"')]),
            ("Тексты", [("text", 1), ("text", 2), ("text", 3)]),
        ],
        home="ASDF JKL;",
    ),
}


# --- Вспомогательное ----------------------------------------------------------------------------------------
def clip(s, maxlen=64):
    s = s.strip()
    if len(s) <= maxlen:
        return s
    cut = s[:maxlen]
    sp = cut.rfind(" ")
    return (cut[:sp] if sp > 0 else cut).strip()


def uniq(lines):
    seen, out = set(), []
    for l in lines:
        if l and l not in seen:
            seen.add(l)
            out.append(l)
    return out


def fill_to(units, rng, minlen=40, maxlen=64):
    """Склеивает единицы через пробел, пока не наберётся длина; обрезает по границе слова."""
    units = list(units)
    out, i = [], 0
    while len(" ".join(out)) < minlen and units:
        out.append(units[i % len(units)])
        i += 1
        if i > 200:
            break
    return clip(" ".join(out), maxlen)


def rhythm_line(rhythm, minlen=36, maxlen=64):
    """Все новые клавиши по очереди; если строка короткая — повторяем серию."""
    line = " ".join(rhythm)
    while len(line) < minlen:
        line += " " + " ".join(rhythm)
    return clip(line, maxlen)


def finger_desc(chars, finger):
    seen, parts = set(), []
    for ch in chars:
        f = finger[ch]
        parts.append(f"{ch.upper()} — {FINGER_NAMES[f]}")
        seen.add(f)
    return "; ".join(parts)


# --- Генераторы упражнений ----------------------------------------------------------------------------------
def make_syllables(letters, vowels, n, rng, must=""):
    """Трёхбуквенные «слоги»: чередуем согласные и гласные, если есть и те и другие."""
    v = [c for c in letters if c in vowels]
    c_ = [c for c in letters if c not in vowels]
    out = []
    tries = 0
    while len(out) < n and tries < 500:
        tries += 1
        if v and c_:
            pat = rng.choice(["cvc", "vcv", "cvv", "vcc", "cvc"])
            s = "".join(rng.choice(v if p == "v" else c_) for p in pat)
        else:
            s = "".join(rng.choice(letters) for _ in range(3))
        if must and not any(m in s for m in must):
            continue
        out.append(s)
    return uniq(out)


def letters_lesson(lang, spec, idx, known, new, words, rng):
    vowels = spec["vowels"]
    known_l = list(known)
    new_l = list(new)
    ex = []

    # 1. Ритм: новые клавиши сериями 2-3-2-3
    rhythm = []
    for x in new_l:
        rhythm += [x * 2, x * 3, x * 2, x * 3]
    ex.append(rhythm_line(rhythm))

    # 2. Пары: новая клавиша со всеми изученными
    pairs = []
    for x in new_l:
        for k in known_l:
            if k != x:
                pairs += [x + k, k + x]
    pairs = uniq(pairs)
    rng.shuffle(pairs)
    ex.append(clip(" ".join(pairs[:14]), 64))

    # 3. Слоги с новыми клавишами
    syl = make_syllables(known_l, vowels, 12, rng, must="".join(new_l))
    ex.append(clip(" ".join(syl[:12]), 64))

    # 4-5. Настоящие слова из изученных букв (приоритет — со свежими клавишами)
    kn = set(known_l)
    ok = [w for w in words if set(w) <= kn and len(w) <= 9]
    with_new = [w for w in ok if any(c in w for c in new_l)]
    others = [w for w in ok if w not in with_new]
    rng.shuffle(with_new)
    rng.shuffle(others)
    if len(with_new) + len(others) >= 4:
        pool1 = (with_new + others)[:9]
        line1 = clip(" ".join(pool1), 64)
        rest = with_new[4:] + others[5:] if len(with_new) > 4 else others[5:] + with_new
        pool2 = (with_new[:3] + rest)[:9] if with_new else rest[:9]
        rng.shuffle(pool2)
        ex.append(line1)
        ex.append(clip(" ".join(pool2 if pool2 else pool1[::-1]), 64))
    else:
        quads = make_syllables(known_l, vowels, 12, rng, must="".join(new_l))
        ex.append(clip(" ".join(q + rng.choice(known_l) for q in quads[:9]), 64))
        ex.append(clip(" ".join(q + rng.choice(new_l) for q in quads[:9]), 64))

    # 6. Ритм пар: каждая пара трижды
    rp = [p for p in pairs[:5]]
    ex.append(clip(" ".join(f"{p} {p} {p}" for p in rp), 64))

    ex = uniq(ex)
    return ex


def caps_lesson(lang, spec, side, known_letters, finger, words, names, places, rng):
    vowels = spec["vowels"]

    def on_side(ch):
        f = finger[ch.lower()]
        return f <= 3 if side == "left" else f >= 4

    never_first = set("ъыь")  # с этих букв слова не начинаются
    side_letters = [c for c in known_letters if on_side(c) and c not in never_first]
    lower_other = list(known_letters)
    ex = []

    # 1. Заглавная + строчная
    units = [x.upper() + rng.choice([c for c in lower_other if c != x]) for x in rng.sample(side_letters, min(len(side_letters), 14))]
    ex.append(clip(" ".join(units), 64))
    # 2. Заглавная + две строчные
    units = [x.upper() + "".join(rng.choice(lower_other) for _ in range(2)) for x in rng.sample(side_letters, min(len(side_letters), 12))]
    ex.append(clip(" ".join(units), 64))

    # 3-6. Слова, имена, названия с заглавной на нужной стороне
    cap_words = [w.capitalize() for w in words if len(w) <= 8 and on_side(w[0]) and set(w) <= set(known_letters)]
    rng.shuffle(cap_words)
    ex.append(clip(" ".join(cap_words[:9]), 64))
    nm = [n for n in names if on_side(n[0]) and n[0].lower() in finger]
    rng.shuffle(nm)
    ex.append(clip(" ".join(nm[:8]), 64))
    pl = [p for p in places if on_side(p[0]) and p[0].lower() in finger]
    rng.shuffle(pl)
    ex.append(clip(" ".join(pl[:7]), 64))
    mix = nm[8:12] + pl[7:11] + cap_words[9:13]
    rng.shuffle(mix)
    ex.append(clip(" ".join(mix), 64))
    return uniq([e for e in ex if e])


def digits_lesson(lang, spec, new, known_digits, words, rng):
    kd = list(known_digits)
    nd = list(new)
    ex = []

    rhythm = []
    for x in nd:
        rhythm += [x * 2, x * 3, x * 2, x * 3]
    ex.append(rhythm_line(rhythm))

    pairs = []
    for x in nd:
        for k in kd:
            if k != x:
                pairs += [x + k, k + x]
    pairs = uniq(pairs)
    rng.shuffle(pairs)
    ex.append(clip(" ".join(pairs[:14]), 64))

    def num(n, must=True):
        while True:
            s = "".join(rng.choice(kd) for _ in range(n))
            if not must or any(d in s for d in nd):
                return s

    ex.append(clip(" ".join(num(4) for _ in range(10)), 64))
    short_words = [w for w in words if 2 <= len(w) <= 6]
    ex.append(clip(" ".join(f"{num(rng.choice([1, 2, 3]))} {rng.choice(short_words)}" for _ in range(7)), 64))
    ex.append(clip(" ".join(num(rng.choice([2, 3, 4, 5]), must=False) for _ in range(10)), 64))
    ex.append(clip(" ".join(f"{rng.choice(short_words)} {num(rng.choice([2, 3]))}" for _ in range(7)), 64))
    return uniq(ex)


CONTRACTIONS = ["it's", "don't", "can't", "won't", "let's", "that's", "what's", "isn't", "didn't",
                "we're", "you're", "they're", "he's", "she's", "there's", "wasn't"]

PUNCT_TEMPLATES = {
    ".": ("{w}.", "точка"), ",": ("{w},", "запятая"), "'": ("{w}'s", "апостроф"),
    "!": ("{w}!", "восклицательный знак"), "?": ("{w}?", "вопросительный знак"),
    ":": ("{w}:", "двоеточие"), ";": ("{w};", "точка с запятой"),
    "-": ("{w}-{w2}", "дефис"), "(": ("({w})", "скобки"), '"': ('"{w}"', "кавычки"),
}


def punct_lesson(lang, spec, marks, allowed, sentences, words, rng):
    short = [w for w in words if 2 <= len(w) <= 6 and set(w) <= allowed]
    ex = []
    templates = []
    for m in marks:
        if m == ")":
            continue
        templates.append(PUNCT_TEMPLATES[m][0])

    # По одной строке на каждый знак (на коротких словах)
    for t in templates:
        if t == "{w}'s":
            pool = [c for c in CONTRACTIONS if set(c) <= allowed]
            rng.shuffle(pool)
            ex.append(clip(" ".join(pool[:9]), 64))
            continue
        items = [t.format(w=rng.choice(short), w2=rng.choice(short)) for _ in range(8)]
        ex.append(clip(" ".join(items), 64))
    # Все знаки вперемешку
    plain = [t for t in templates if t != "{w}'s"]
    mixed = [rng.choice(plain).format(w=rng.choice(short), w2=rng.choice(short)) for _ in range(9)]
    ex.append(clip(" ".join(mixed), 64))
    # Живые предложения с этими знаками
    cur = [m for m in marks]
    sents = [s for s in sentences if set(s) <= allowed and any(m in s for m in cur)]
    rng.shuffle(sents)
    ex.extend(clip(s, 110) if len(s) <= 110 else s for s in sents[:2])
    return uniq(ex)


def text_lesson(level, sentences, rng):
    ss = [s for s in sentences]
    rng.shuffle(ss)
    if level == 1:
        pick = sorted(ss, key=len)[:6]           # короткие предложения
        return uniq(pick)
    if level == 2:
        pick = ss[:10]
        return uniq([f"{pick[i]} {pick[i + 1]}" for i in range(0, 10, 2)])
    pick = ss[:12]
    return uniq([f"{pick[i]} {pick[i + 1]} {pick[i + 2]}" for i in range(0, 12, 3)])


# --- Сборка курса -------------------------------------------------------------------------------------------
def criteria_for(kind, n_overall, tlevel=0):
    if kind == "letters":
        if n_overall <= 3:
            return 60, 95
        if n_overall <= 10:
            return 80, 95
        return 90, 96
    if kind == "caps":
        return 90, 96
    if kind == "digits":
        return 70, 95
    if kind == "punct":
        return 80, 96
    return (110, 97) if tlevel == 1 else ((130, 97) if tlevel == 2 else (150, 97))


def build_course(lang):
    spec = LANGS[lang]
    typeable, finger = layout_tables(lang)
    rng = random.Random(f"dexter-{lang}-v2")
    words = [w for w in read_words(f"{lang}_words.txt") if all(c in typeable for c in w)]
    # нижний регистр и без дубликатов, сохраняя порядок
    words = list(dict.fromkeys(w for w in words if w == w.lower()))
    names = read_lines(f"{lang}_names.txt")[0].split() if False else read_words(f"{lang}_names.txt")
    places = read_words(f"{lang}_places.txt")
    sentences = read_lines(f"{lang}_sentences.txt")

    known = ""            # изученные строчные буквы
    known_digits = ""
    allowed_extra = set()  # заглавные, цифры, знаки — по мере изучения
    parts_out = []
    n = 0
    all_letters = None

    for part_title, lessons in spec["parts"]:
        lessons_out = []
        for kind, arg in lessons:
            n += 1
            lid = f"{lang}-{n:02d}"
            intro, title, new_keys, tlevel = "", "", "", 0

            if kind == "letters":
                new = arg
                known += new
                ex = letters_lesson(lang, spec, n, known, new, words, rng)
                title = "Клавиши " + " ".join(c.upper() for c in new)
                new_keys = new.upper()
                intro = (f"Новые клавиши: {finger_desc(new, finger)}. "
                         f"Держите пальцы на домашнем ряду ({spec['home']}), большие пальцы — над пробелом. "
                         f"Не смотрите на клавиатуру.")
                allowed = set(known) | {" "}
            elif kind == "caps":
                all_letters = [c for c in known if c.isalpha()]
                side = arg
                ex = caps_lesson(lang, spec, side, all_letters, finger, words, names, places, rng)
                if side == "left":
                    title, new_keys = "Заглавные: буквы левой руки", ""
                    intro = ("Заглавные буквы левой руки печатаются с правым Shift: зажмите его правым мизинцем, "
                             "нажмите букву левой рукой и отпустите оба пальца.")
                    allowed_extra |= {c.upper() for c in all_letters if finger[c] <= 3}
                else:
                    title = "Заглавные: буквы правой руки"
                    intro = ("Заглавные буквы правой руки печатаются с левым Shift: зажмите его левым мизинцем, "
                             "нажмите букву правой рукой и отпустите оба пальца.")
                    allowed_extra |= {c.upper() for c in all_letters if finger[c] >= 4}
                allowed = set(known) | allowed_extra | {" "}
            elif kind == "digits":
                new = arg
                known_digits += new
                ex = digits_lesson(lang, spec, new, known_digits, words, rng)
                title = "Цифры " + " ".join(new)
                new_keys = new
                intro = (f"Цифры: {finger_desc(new, finger)}. "
                         f"После каждой цифры возвращайте палец на домашний ряд.")
                allowed_extra |= set(new)
                allowed = set(known) | allowed_extra | {" "}
            elif kind == "punct":
                marks = arg
                allowed_extra |= set(marks)
                allowed = set(known) | allowed_extra | {" "}
                ex = punct_lesson(lang, spec, marks, allowed, sentences, words, rng)
                names_ = [PUNCT_TEMPLATES[m][1] for m in marks if m != ")"]
                title = "Знаки: " + " ".join(marks)
                new_keys = marks
                intro = ("Знаки препинания: " + ", ".join(names_) + ". "
                         "Знак ставится сразу за словом, без пробела перед ним. Для знаков на верхнем регистре держите Shift.")
            else:  # text
                tlevel = arg
                allowed = None
                ex = text_lesson(tlevel, sentences, rng)
                title = {1: "Предложения", 2: "Фразы подряд", 3: "Длинные тексты"}[tlevel]
                intro = "Печатайте ровно и без спешки. Следите за точностью: скорость придёт сама."

            # --- проверка: только изученные символы, только то, что есть на клавиатуре -------------------
            for line in ex:
                bad = {c for c in line if c not in typeable}
                assert not bad, f"{lid}: символы вне раскладки {bad} в «{line}»"
                if allowed is not None:
                    bad = {c for c in line if c not in allowed}
                    assert not bad, f"{lid}: ещё не изученные символы {bad} в «{line}»"
                assert line == line.strip() and len(line) >= 3, f"{lid}: плохая строка «{line}»"
            assert len(ex) >= 4, f"{lid}: слишком мало упражнений ({len(ex)})"

            min_cpm, min_acc = criteria_for(kind, n, tlevel)
            lessons_out.append(dict(id=lid, title=title, newKeys=new_keys, intro=intro, exercises=ex,
                                    minCpm=min_cpm, minAccuracy=min_acc))
        parts_out.append(dict(title=part_title, lessons=lessons_out))

    return dict(id=spec["id"], title=spec["title"], layout=lang,
                criteria=dict(minCpm=100, minAccuracy=96), parts=parts_out)


def check_file(path, lang):
    typeable, _ = layout_tables(lang)
    with open(path, encoding="utf-8") as f:
        course = json.load(f)
    problems = 0
    for p in course["parts"]:
        for l in p["lessons"]:
            for i, line in enumerate(l["exercises"], 1):
                bad = {c for c in line if c not in typeable}
                if bad:
                    problems += 1
                    print(f"  {l['id']}[{i}]: символы вне раскладки {bad}")
    return course, problems


def main():
    check_only = "--check" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    total_problems = 0
    for lang in ("ru", "en"):
        path = os.path.join(OUT, f"{lang}-basic.json")
        if not check_only:
            course = build_course(lang)
            with open(path, "w", encoding="utf-8") as f:
                json.dump(course, f, ensure_ascii=False, indent=2)
                f.write("\n")
        course, problems = check_file(path, lang)
        total_problems += problems
        lessons = [l for p in course["parts"] for l in p["lessons"]]
        lines = sum(len(l["exercises"]) for l in lessons)
        print(f"{os.path.basename(path)}: {len(course['parts'])} ступеней, {len(lessons)} уроков, {lines} упражнений, проблем: {problems}")
    sys.exit(1 if total_problems else 0)


if __name__ == "__main__":
    main()
