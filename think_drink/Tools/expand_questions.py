"""Rebuild question_bank.json from curated seeds + bundled OpenTDB dump (real trivia)."""

import html
import json
import re
from pathlib import Path

TARGET_COUNT = 5000
CATEGORIES = [
    "Animals",
    "Food",
    "Gaming",
    "Geography",
    "History",
    "Math",
    "Movies",
    "Science",
    "Sports",
]

DIFFICULTY_MAP = {
    "easy": "Easy",
    "medium": "Medium",
    "hard": "Hard",
}

ROOT = Path(__file__).resolve().parents[1]
BANK_PATH = ROOT / "Assets" / "data" / "question_bank.json"
DUMP_PATH = ROOT / "Tools" / "opentdb_dump.json"


def clean_text(value: str) -> str:
    if not value:
        return ""
    text = html.unescape(value)
    text = re.sub(r"\s+", " ", text).strip()
    return text


def normalize_question(q):
    q.setdefault("Alternatives", [])
    q.setdefault("Distractors", [])
    q.setdefault("Tags", [])
    q.setdefault("Explanation", "")
    return q


def rephrase_open_question(question: str) -> str:
    text = clean_text(question)
    if not text:
        return text

    replacements = [
        (r"^Which of the following\s+(is|are|was|were)\s+", r"What \1 "),
        (r"^Which of these\s+(is|are|was|were)\s+", r"What \1 "),
        (r"^Which one of the following\s+(is|are|was|were)\s+", r"What \1 "),
        (r"^Which one of these\s+(is|are|was|were)\s+", r"What \1 "),
        (r"^Select the correct answer:\s*", ""),
        (r"^Pick the correct answer:\s*", ""),
        (r"\s+from the following options\.?$", ""),
        (r"\s+from the following\.?$", ""),
    ]

    for pattern, repl in replacements:
        text = re.sub(pattern, repl, text, count=1, flags=re.IGNORECASE)

    if text and text[0].islower():
        text = text[0].upper() + text[1:]
    return text


def finalize_question(q):
    distractors = q.get("Distractors") or []
    if not distractors:
        q["Question"] = rephrase_open_question(q.get("Question", ""))
    return q


def map_opentdb_category(source: str) -> str | None:
    name = clean_text(source).lower()

    if name == "animals":
        return "Animals"
    if name == "geography":
        return "Geography"
    if name == "sports":
        return "Sports"
    if name in {"history", "politics", "mythology", "art"}:
        return "History"
    if name == "science: mathematics":
        return "Math"
    if name.startswith("science") or name == "vehicles":
        return "Science"
    if name in {
        "entertainment: video games",
        "entertainment: board games",
        "entertainment: japanese anime & manga",
        "entertainment: comics",
    }:
        return "Gaming"
    if name in {
        "entertainment: film",
        "entertainment: music",
        "entertainment: musicals & theatres",
        "entertainment: television",
        "entertainment: cartoon & animations",
        "entertainment: books",
        "celebrities",
    }:
        return "Movies"
    if name == "general knowledge":
        return None
    return None


def is_food_question(question: str) -> bool:
    text = question.lower()
    keywords = (
        "food",
        "fruit",
        "vegetable",
        "cheese",
        "coffee",
        "tea ",
        "wine",
        "beer",
        "candy",
        "chocolate",
        "pizza",
        "burger",
        "sandwich",
        "sushi",
        "bread",
        "spice",
        "recipe",
        "cook",
        "kitchen",
        "restaurant",
        "breakfast",
        "dinner",
        "lunch",
        "snack",
        "nut ",
        "nuts ",
        "meat",
        "fish",
        "egg",
        "milk",
        "sugar",
        "salt",
        "pepper",
        "apple",
        "banana",
        "potato",
        "rice",
        "pasta",
        "whiskey",
        "vodka",
        "rum",
        "cocktail",
    )
    return any(word in text for word in keywords)


def add_question(questions, existing_ids, existing_text, qid, cat, diff, question, accepted, explanation="", alts=None, tags=None, distractors=None):
    key = question.strip().lower()
    if qid in existing_ids or key in existing_text:
        return False

    questions.append(
        {
            "Id": qid,
            "Category": cat,
            "Difficulty": diff,
            "Question": question,
            "Accepted": [str(a) for a in accepted],
            "Alternatives": [str(a) for a in (alts or [])],
            "Distractors": [str(d) for d in (distractors or [])],
            "Tags": tags or ["opentdb", cat.lower()],
            "Explanation": explanation,
        }
    )
    existing_ids.add(qid)
    existing_text.add(key)
    return True


def load_curated():
    data = json.loads(BANK_PATH.read_text(encoding="utf-8"))
    curated = []
    for q in data.get("Questions", []):
        qid = str(q.get("Id", ""))
        if qid.startswith("gen_") or qid.startswith("otdb_") or qid.startswith("food_seed_"):
            continue
        curated.append(normalize_question(q))
    return curated


def food_seed_questions():
    return [
        ("What country is sushi from?", "Japan"),
        ("What fruit is known for keeping the doctor away?", "Apple", ["Apples"]),
        ("What nut is used to make marzipan?", "Almond", ["Almonds"]),
        ("What cheese is traditionally used on a Margherita pizza?", "Mozzarella"),
        ("What grain is used to make traditional Italian risotto?", "Rice"),
        ("What is the main ingredient in hummus?", "Chickpeas", ["Chickpea", "Garbanzo beans"]),
        ("What country did croissants originate from?", "France"),
        ("What type of pasta is shaped like small rice grains?", "Orzo"),
        ("What spice gives curry its yellow color?", "Turmeric"),
        ("What is the primary ingredient in guacamole?", "Avocado", ["Avocados"]),
        ("What country is feta cheese most associated with?", "Greece"),
        ("What meat is used in traditional wiener schnitzel?", "Veal"),
        ("What is tofu made from?", "Soybeans", ["Soy", "Soybean"]),
        ("What fruit is used to make traditional cider?", "Apple", ["Apples"]),
        ("What country is paella from?", "Spain"),
        ("What is the main alcohol in a mojito?", "Rum"),
        ("What leaf is used to wrap dolmas?", "Grape leaves", ["Grape leaf"]),
        ("What is the Italian word for a coffee with a small amount of milk?", "Macchiato"),
        ("What vegetable is the main ingredient in borscht?", "Beet", ["Beets", "Beetroot"]),
        ("What country is kimchi most associated with?", "Korea", ["South Korea"]),
        ("What nut is used in pesto alongside basil?", "Pine nut", ["Pine nuts"]),
        ("What is the main ingredient in traditional miso soup paste?", "Soybeans", ["Soy"]),
        ("What citrus fruit is key to key lime pie?", "Lime", ["Key lime"]),
        ("What country invented pizza in its modern form?", "Italy"),
        ("What grain is used to make bourbon whiskey?", "Corn"),
        ("What is the main ingredient in falafel?", "Chickpeas", ["Chickpea"]),
        ("What type of tea is traditionally served with milk in Britain?", "Black tea", ["English breakfast tea"]),
        ("What fruit is dried to make a prune?", "Plum", ["Plums"]),
        ("What country is goulash from?", "Hungary"),
        ("What is the main ingredient in tzatziki?", "Yogurt", ["Greek yogurt"]),
        ("What fish is traditionally used in fish and chips?", "Cod", ["Haddock"]),
        ("What is the main starch in gnocchi?", "Potato", ["Potatoes"]),
        ("What country is brie cheese from?", "France"),
        ("What spice is derived from crocus flowers?", "Saffron"),
        ("What bean is used to make chocolate?", "Cacao", ["Cocoa"]),
        ("What is the main ingredient in polenta?", "Cornmeal", ["Corn"]),
        ("What country is pho from?", "Vietnam"),
        ("What meat is used in corned beef?", "Beef"),
        ("What is the main ingredient in tahini?", "Sesame seeds", ["Sesame"]),
        ("What fruit is used to make wine?", "Grape", ["Grapes"]),
        ("What country is the origin of tacos?", "Mexico"),
        ("What is the main ingredient in a traditional omelette?", "Eggs", ["Egg"]),
        ("What nut is used in Nutella besides cocoa?", "Hazelnut", ["Hazelnuts"]),
        ("What is the main ingredient in sauerkraut?", "Cabbage"),
        ("What country is known for inventing tempura?", "Japan"),
        ("What grain is used to make sake?", "Rice"),
        ("What country is wiener schnitzel most associated with?", "Austria"),
        ("What is the main ingredient in meringue?", "Egg whites", ["Egg white"]),
        ("What fruit is a Cavendish variety of?", "Banana", ["Bananas"]),
    ]


def import_food_seeds(questions, existing_ids, existing_text):
    added = 0
    for index, row in enumerate(food_seed_questions(), start=1):
        question, answer, *rest = row
        alts = rest[0] if rest else []
        qid = f"food_seed_{index:04d}"
        if add_question(
            questions,
            existing_ids,
            existing_text,
            qid,
            "Food",
            "Medium",
            question,
            [answer],
            "Curated food trivia.",
            alts=alts,
            tags=["food", "curated"],
        ):
            added += 1
    return added


def import_dump(questions, existing_ids, existing_text):
    if not DUMP_PATH.exists():
        raise FileNotFoundError(f"Missing OpenTDB dump: {DUMP_PATH}")

    dump = json.loads(DUMP_PATH.read_text(encoding="utf-8"))
    imported = 0
    general = []

    for item in dump:
        question = clean_text(item.get("question", ""))
        answer = clean_text(item.get("correct_answer", ""))
        if not question or not answer:
            continue

        source = clean_text(item.get("category", ""))
        target = map_opentdb_category(source)
        if target is None:
            general.append(item)
            continue

        diff = DIFFICULTY_MAP.get(item.get("difficulty", "medium"), "Medium")
        qid = f"otdb_{target.lower()}_{len(existing_ids):05d}"
        explanation = f"Sourced from Open Trivia DB ({source})."
        distractors = [clean_text(x) for x in item.get("incorrect_answers", []) if clean_text(x)]
        if add_question(
            questions,
            existing_ids,
            existing_text,
            qid,
            target,
            diff,
            question,
            [answer],
            explanation,
            tags=["opentdb", target.lower(), source.lower()],
            distractors=distractors,
        ):
            imported += 1

    fill_targets = [cat for cat in CATEGORIES if cat != "Math"]
    fill_index = 0
    for item in general:
        question = clean_text(item.get("question", ""))
        answer = clean_text(item.get("correct_answer", ""))
        if not question or not answer:
            continue

        if is_food_question(question):
            target = "Food"
        else:
            target = fill_targets[fill_index % len(fill_targets)]
            fill_index += 1

        diff = DIFFICULTY_MAP.get(item.get("difficulty", "medium"), "Medium")
        qid = f"otdb_{target.lower()}_{len(existing_ids):05d}"
        source = clean_text(item.get("category", ""))
        explanation = f"Sourced from Open Trivia DB ({source})."
        distractors = [clean_text(x) for x in item.get("incorrect_answers", []) if clean_text(x)]
        if add_question(
            questions,
            existing_ids,
            existing_text,
            qid,
            target,
            diff,
            question,
            [answer],
            explanation,
            tags=["opentdb", target.lower(), "general"],
            distractors=distractors,
        ):
            imported += 1

    return imported


def balance_categories(questions):
    by_cat = {cat: [] for cat in CATEGORIES}
    for q in questions:
        cat = q.get("Category", "Science")
        if cat not in by_cat:
            by_cat[cat] = []
        by_cat[cat].append(q)

    per_cat = max(200, TARGET_COUNT // len(CATEGORIES))
    balanced = []
    for cat in CATEGORIES:
        pool = sorted(by_cat.get(cat, []), key=lambda q: q["Id"])
        balanced.extend(pool[:per_cat])

    if len(balanced) < TARGET_COUNT:
        remainder = []
        for cat in CATEGORIES:
            pool = sorted(by_cat.get(cat, []), key=lambda q: q["Id"])
            remainder.extend(pool[per_cat:])
        balanced.extend(remainder[: TARGET_COUNT - len(balanced)])

    return balanced[:TARGET_COUNT]


def main():
    questions = load_curated()
    existing_ids = {q["Id"] for q in questions}
    existing_text = {q["Question"].strip().lower() for q in questions}

    print(f"Starting with {len(questions)} curated questions.")
    food_added = import_food_seeds(questions, existing_ids, existing_text)
    print(f"Added {food_added} curated food questions.")

    imported = import_dump(questions, existing_ids, existing_text)
    print(f"Imported {imported} questions from OpenTDB dump.")

    final = balance_categories(questions)
    final = [finalize_question(q) for q in final]
    counts = {cat: sum(1 for q in final if q["Category"] == cat) for cat in CATEGORIES}
    mcq_count = sum(1 for q in final if q.get("Distractors"))
    rephrased = sum(1 for q in final if not q.get("Distractors") and re.search(r"which of (the following|these)", q.get("Question", ""), re.I))

    data = {"Version": 5, "Questions": final}
    BANK_PATH.write_text(json.dumps(data, indent=2), encoding="utf-8")

    print(f"Question bank now has {len(final)} questions.")
    print(f"  MCQ-ready (with distractors): {mcq_count}")
    print(f"  Remaining MCQ-framed open questions: {rephrased}")
    for cat in CATEGORIES:
        print(f"  {cat}: {counts.get(cat, 0)}")

    sample = next((q for q in final if q["Category"] == "Geography"), None)
    if sample:
        print(f"Sample geography question: {sample['Question']}")


if __name__ == "__main__":
    main()
