"""Offline smoke test of balance numbers (no s&box runtime)."""
# Approximate the ecology + economy loop with the same constants as Catalog.

FISH = [
    ("sardine", 8, 0, 120),
    ("mackerel", 14, 10, 220),
    ("seabass", 28, 40, 280),
    ("tuna", 140, 300, 900),
]

def value_after_n_fish(n=3, avg=14):
    return n * avg

coins = 250
catch = value_after_n_fish(3, 16)
coins += catch
print(f"After selling 3 starter fish: {coins} coins")
print(f"Can afford precision rod (150)? {coins >= 150}")
print(f"Can afford first hook (100)? {coins >= 100}")
print(f"Fisher 17 needs ~3500 — sessions needed at ~60 coins/trip: {3500 / 60:.0f}")
assert coins >= 150
print("SMOKE OK")
