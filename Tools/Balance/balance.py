"""
100-level balance model for ShooterGame (Squad Shooter template).

Writes balance.json, which the Unity editor tool `Tools/Balance/Import Balance` applies to the
weapon / character / experience / level assets, and which the room generator reads for new levels.

Model
-----
* Strength is a unitless multiplier. A weapon at stage s has strength
      baseStrength_w * STAGE_GROWTH ** (s - 1)
  and an enemy's HP is normalised to the weapon's stage-1 damage, multiplied by the level's
  enemyHpMult and divided by the weapon's baseStrength. So "strength 2" kills twice as fast.
* Characters work the same way for health: health = 100 * baseStrength_c * HP_GROWTH ** (s - 1)
  and enemy damage is normalised to 100 HP, multiplied by enemyDmgMult.
* A simulated player clears levels in order, collects coins/cards/XP and greedily buys the
  upgrade with the best strength-per-coin. Their strength is the reference E(n); the level
  multipliers are E(n) * pressure(n). Medium-hard ("orta-zor"): pressure ramps inside every
  10-level block, so the last levels before a boss demand being fully upgraded.

Run:  python3 Tools/Balance/balance.py   (prints a summary table, writes balance.json)
"""

import json
import math
import os

LEVELS = 100
LEVELS_PER_WORLD = 20
WORLD_NAMES = ["Desert Canyon", "Crystal Mine", "Glacier Base", "Riot City", "Volcano Factory"]

STAGE_GROWTH = 1.12        # weapon strength per upgrade
DAMAGE_SHARE = 0.75        # part of the weapon growth that goes into damage (rest = fire rate)
HP_GROWTH = 1.08           # character health per upgrade
WEAPON_SHARE = 0.6         # part of the income a typical player spends on weapons
WEAPON_STAGES = 15
CHARACTER_STAGES = 10

# name, rarity (0 common .. 4 legendary), baseStrength, stage-1 damage (min, max), fire rate,
# first upgrade price, price growth, cards to unlock, unlock level
WEAPONS = [
    ("MINIGUN",        0, 1.00, (5, 15),  10.0, 60,  1.20, 0,  1),
    ("SHOTGUN",        0, 1.45, (35, 40), 0.8,  90,  1.20, 5,  6),
    ("LAVA GUN",       1, 3.10, (20, 35), 1.0,  220, 1.20, 10, 24),
    ("CRYO GUN",       2, 5.20, (18, 26), 5.0,  480, 1.20, 10, 44),
    ("TESLA GUN",      4, 8.40, (8, 15),  2.1,  950, 1.20, 12, 66),
    ("ROCKET LAUNCHER", 3, 13.0, (60, 80), 0.7, 1700, 1.20, 12, 84),
]

# Raw single-target DPS differs a lot between weapon types (minigun ~100, tesla ~24 at stage 1); enemy HP is scaled
# by each weapon's stage-1 DPS relative to the minigun, times a bonus for area / chain / slow effects, so equal
# strength means roughly equal time-to-kill whatever the weapon.
BULLETS_PER_SHOT = {"SHOTGUN": 3}
EFFECT_BONUS = {"LAVA GUN": 2.0, "TESLA GUN": 2.5, "ROCKET LAUNCHER": 2.0, "CRYO GUN": 1.15}


def dps_factor(w):
    name, rarity, base, dmg, fr = w[0], w[1], w[2], w[3], w[4]
    raw = (dmg[0] + dmg[1]) / 2 * fr * BULLETS_PER_SHOT.get(name, 1)
    reference = (WEAPONS[0][3][0] + WEAPONS[0][3][1]) / 2 * WEAPONS[0][4]
    return round(raw / reference * EFFECT_BONUS.get(name, 1.0), 3)


# name, baseStrength (health factor), first upgrade price, price growth, required XP level
CHARACTERS = [
    ("JIM",    1.00, 60,   1.22, 1),
    ("MIKE",   1.40, 260,  1.22, 3),
    ("DUSTIN", 2.05, 650,  1.22, 6),
    ("FROST",  2.75, 1400, 1.22, 10),
    ("BLAZE",  3.70, 2800, 1.22, 15),
]

# levels at which a character should unlock (XP level reached) -> drives the XP curve
CHARACTER_UNLOCK_LEVELS = {3: 12, 6: 30, 10: 50, 15: 80}
MAX_XP_LEVEL = 20


def level_info(n):
    world = (n - 1) // LEVELS_PER_WORLD
    in_world = (n - 1) % LEVELS_PER_WORLD + 1
    block_pos = (in_world - 1) % 10            # 0..9 inside a 10-level block
    boss = block_pos == 9
    return world, in_world, block_pos, boss


def rooms_for(n, boss):
    if boss:
        return 1
    if n == 1:
        return 1
    if n <= 4:
        return 2
    if n <= 50:
        return 3
    return 4 if (n % 3 == 0) else 3


def coins_for(n, boss):
    base = 80 * 1.032 ** (n - 1)
    return int(round(base * (1.8 if boss else 1.0) / 5) * 5)


def xp_for(n, boss):
    return int(round((100 + 3 * n) * (1.5 if boss else 1.0) / 5) * 5)


def pressure(n):
    """Medium-hard difficulty shape: sawtooth per 10 levels, slight global ramp, eased world entry."""
    world, in_world, block_pos, boss = level_info(n)
    saw = 0.88 + 0.035 * block_pos                     # 0.88 .. 1.195
    if boss:
        saw = 1.05                                       # bosses carry their own HP
    if in_world <= 2 and world > 0:
        saw -= 0.06                                      # breathe after a new world opens
    ramp = 1.0 + 0.0015 * n
    tutorial = min(1.0, 0.55 + 0.09 * n)                 # first levels are gentle
    return saw * ramp * tutorial


def build_xp_curve():
    xp_cum = [0]
    for n in range(1, LEVELS + 1):
        xp_cum.append(xp_cum[-1] + xp_for(n, level_info(n)[3]))
    anchors = {1: 0}
    for lvl, at_level in CHARACTER_UNLOCK_LEVELS.items():
        anchors[lvl] = xp_cum[at_level - 1]              # reached right after clearing level-1
    anchors[MAX_XP_LEVEL] = int(xp_cum[LEVELS] * 1.05)
    keys = sorted(anchors)
    thresholds = []
    for lvl in range(1, MAX_XP_LEVEL + 1):
        for a, b in zip(keys, keys[1:]):
            if a <= lvl <= b:
                t = (lvl - a) / (b - a)
                thresholds.append(int(round((anchors[a] + (anchors[b] - anchors[a]) * t) / 10) * 10))
                break
    return thresholds, xp_cum


def card_drops():
    """Cards drop in the levels just before a weapon's unlock level, sum = cards to unlock."""
    drops = {}
    for name, _, _, _, _, _, _, cards, unlock in WEAPONS:
        if cards == 0:
            continue
        levels = [l for l in range(unlock - 6, unlock + 1) if not level_info(l)[3]][-min(cards, 5):]
        per = [cards // len(levels)] * len(levels)
        for i in range(cards - sum(per)):
            per[-1 - i] += 1
        for l, amount in zip(levels, per):
            drops.setdefault(l, []).append({"weapon": name, "amount": amount})
    return drops


def weapon_stage(w, s):
    name, rarity, base, dmg, fr, p0, pg, cards, unlock = w
    strength = base * STAGE_GROWTH ** (s - 1)
    growth = STAGE_GROWTH ** (s - 1)
    dmg_mult = growth ** DAMAGE_SHARE
    fr_mult = growth ** (1 - DAMAGE_SHARE)
    price = 0 if s == 1 else int(round(p0 * pg ** (s - 2) / 5) * 5)
    return {
        "stage": s,
        "price": price if s > 1 else cards,              # stage 1 price = cards to unlock (template convention)
        "damage": [max(1, int(round(dmg[0] * dmg_mult))), max(1, int(round(dmg[1] * dmg_mult)))],
        "fireRate": round(fr * fr_mult, 2),
        "strength": round(strength, 3),
        "power": int(round(strength * 10)),
    }


def character_stage(c, s):
    name, base, p0, pg, req = c
    strength = base * HP_GROWTH ** (s - 1)
    price = 0 if s == 1 else int(round(p0 * pg ** (s - 2) / 5) * 5)
    return {"stage": s, "price": price, "health": int(round(100 * strength)), "strength": round(strength, 3),
            "power": int(round(strength * 10))}


def simulate(levels, thresholds, spend_ratio=0.9):
    """Greedy player: buys the upgrade with the best relative strength gain per coin."""
    coins = 0
    xp = 0
    cards = {w[0]: 0 for w in WEAPONS}
    w_stage = {w[0]: (1 if w[7] == 0 else 0) for w in WEAPONS}
    c_stage = {"JIM": 1}
    wallets = {"w": 0.0, "c": 0.0}
    history = []

    def xp_level():
        return sum(1 for t in thresholds if xp >= t)

    def best_weapon():
        owned = [w for w in WEAPONS if w_stage[w[0]] > 0]
        return max(owned, key=lambda w: weapon_stage(w, w_stage[w[0]])["strength"])

    def best_character():
        return max((c for c in CHARACTERS if c[0] in c_stage), key=lambda c: character_stage(c, c_stage[c[0]])["strength"])

    for lv in levels:
        n = lv["n"]
        bw, bc = best_weapon(), best_character()
        ws = weapon_stage(bw, w_stage[bw[0]])["strength"]
        cs = character_stage(bc, c_stage[bc[0]])["strength"]
        history.append({"n": n, "weapon": bw[0], "wStage": w_stage[bw[0]], "wStrength": ws,
                        "character": bc[0], "cStage": c_stage[bc[0]], "cStrength": cs, "coins": coins})

        # clear the level
        coins += lv["coins"]
        xp += lv["xp"]
        for drop in lv["cards"]:
            cards[drop["weapon"]] += drop["amount"]
        for w in WEAPONS:
            if w_stage[w[0]] == 0 and cards[w[0]] >= w[7]:
                w_stage[w[0]] = 1
        for c in CHARACTERS:
            if c[0] not in c_stage and xp_level() >= c[4]:
                c_stage[c[0]] = 1

        # shop: the player splits income between weapons and characters
        wallets["w"] += lv["coins"] * WEAPON_SHARE
        wallets["c"] += lv["coins"] * (1 - WEAPON_SHARE)
        for kind in ("w", "c"):
            while True:
                options = []
                if kind == "w":
                    current = weapon_stage(best_weapon(), w_stage[best_weapon()[0]])["strength"]
                    for w in WEAPONS:
                        s = w_stage[w[0]]
                        if 0 < s < WEAPON_STAGES:
                            nxt = weapon_stage(w, s + 1)
                            if nxt["strength"] > current:
                                options.append(((nxt["strength"] / current) ** (1 / nxt["price"]), nxt["price"], w[0]))
                else:
                    current = character_stage(best_character(), c_stage[best_character()[0]])["strength"]
                    for c in CHARACTERS:
                        s = c_stage.get(c[0], 0)
                        if 0 < s < CHARACTER_STAGES:
                            nxt = character_stage(c, s + 1)
                            if nxt["strength"] > current:
                                options.append(((nxt["strength"] / current) ** (1 / nxt["price"]), nxt["price"], c[0]))
                options = [o for o in options if o[1] <= wallets[kind] * spend_ratio]
                if not options:
                    break
                _, price, name = max(options)
                wallets[kind] -= price
                coins -= price
                if kind == "w":
                    w_stage[name] += 1
                else:
                    c_stage[name] += 1
    return history


def calibrate(levels, thresholds, lead=1.12):
    """Every new weapon / character starts about one upgrade above what the player has when it unlocks."""
    global WEAPONS, CHARACTERS
    WEAPONS = [list(w) for w in WEAPONS]
    CHARACTERS = [list(c) for c in CHARACTERS]

    for i in range(1, len(WEAPONS)):
        for later in WEAPONS[i:]:
            later[2] = 0.001
        history = simulate(levels, thresholds)
        unlock = WEAPONS[i][8]
        WEAPONS[i][2] = round(history[min(unlock, len(history) - 1)]["wStrength"] * lead, 2)

    unlock_at = {lvl: at for lvl, at in CHARACTER_UNLOCK_LEVELS.items()}
    for i in range(1, len(CHARACTERS)):
        for later in CHARACTERS[i:]:
            later[1] = 0.001
        history = simulate(levels, thresholds)
        unlock = unlock_at[CHARACTERS[i][4]]
        CHARACTERS[i][1] = round(history[min(unlock, len(history) - 1)]["cStrength"] * lead, 2)

    WEAPONS = [tuple(w) for w in WEAPONS]
    CHARACTERS = [tuple(c) for c in CHARACTERS]
    print("calibrated weapons:", [(w[0], w[2]) for w in WEAPONS])
    print("calibrated characters:", [(c[0], c[1]) for c in CHARACTERS])


def main():
    thresholds, xp_cum = build_xp_curve()
    drops = card_drops()

    levels = []
    for n in range(1, LEVELS + 1):
        world, in_world, block_pos, boss = level_info(n)
        levels.append({
            "n": n, "world": world, "levelInWorld": in_world, "boss": boss,
            "rooms": rooms_for(n, boss), "coins": coins_for(n, boss), "xp": xp_for(n, boss),
            "cards": drops.get(n, []), "pressure": round(pressure(n), 3),
        })

    calibrate(levels, thresholds)
    history = simulate(levels, thresholds)
    for lv, h in zip(levels, history):
        lv["expectedWeaponStrength"] = round(h["wStrength"], 3)
        lv["expectedCharacterStrength"] = round(h["cStrength"], 3)
        lv["enemyHpMult"] = round(h["wStrength"] * lv["pressure"], 3)
        lv["enemyDmgMult"] = round(h["cStrength"] * lv["pressure"], 3)
        lv["recommendedPower"] = int(round((h["wStrength"] + h["cStrength"]) * 10 * lv["pressure"]))
        lv["sim"] = h

    data = {
        "worlds": WORLD_NAMES,
        "weapons": [{"name": w[0], "rarity": w[1], "baseStrength": w[2], "dpsFactor": dps_factor(w), "cardsToUnlock": w[7], "unlockLevel": w[8],
                     "stages": [weapon_stage(w, s) for s in range(1, WEAPON_STAGES + 1)]} for w in WEAPONS],
        "characters": [{"name": c[0], "baseStrength": c[1], "requiredLevel": c[4],
                        "stages": [character_stage(c, s) for s in range(1, CHARACTER_STAGES + 1)]} for c in CHARACTERS],
        "xpThresholds": thresholds,
        "levels": [{k: v for k, v in lv.items() if k != "sim"} for lv in levels],
    }

    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "balance.json")
    with open(out, "w") as f:
        json.dump(data, f, indent=1)

    print(f"{'lvl':>4} {'w':>2} {'rm':>2} {'coins':>6} {'xp':>4} {'press':>5} {'enemyHP':>7} {'enemyDMG':>8}  player")
    for lv in levels:
        h = lv["sim"]
        mark = "B" if lv["boss"] else " "
        cards = ",".join(f"{c['weapon'][:4]}x{c['amount']}" for c in lv["cards"])
        if lv["n"] % 5 == 0 or lv["n"] <= 3 or lv["cards"] or lv["boss"]:
            print(f"{lv['n']:>4}{mark} {lv['world'] + 1:>2} {lv['rooms']:>2} {lv['coins']:>6} {lv['xp']:>4} {lv['pressure']:>5.2f} "
                  f"{lv['enemyHpMult']:>7.2f} {lv['enemyDmgMult']:>8.2f}  {h['weapon'][:8]} s{h['wStage']} / {h['character']} s{h['cStage']}  {cards}")
    print("total coins", sum(l["coins"] for l in levels), "| xp thresholds", thresholds)
    print("strength path:", " ".join(f"{l['n']}:{l['expectedWeaponStrength']:.1f}/{l['expectedCharacterStrength']:.1f}" for l in levels if l['n'] % 10 == 1))
    print("wrote", out)


if __name__ == "__main__":
    main()
