# Warhammer 40k Helper

A game-day companion for Warhammer 40,000 10th edition (the only edition this app supports). It answers "what do I need to roll?" at the table and keeps the books for a Crusade Campaign between games.

## Language

### Campaign layer

**Crusade Campaign**:
The whole long-running narrative game a group plays: its map, its players, their forces, and every battle fought. One app instance holds one Crusade Campaign.
_Avoid_: campaign of crusade, narrative campaign (as a distinct thing)

**Campaign Map**:
The territory and event layer of a Crusade Campaign, including GM notes. Territories are captured and held by players.
_Avoid_: campaign (on its own, when the map is meant), war map

**Battle**:
One played game inside the Crusade Campaign. A Battle is recorded once and feeds both the Campaign Map and every Crusade Card that took part.
_Avoid_: match, game, battle record (the record is just how a Battle is stored)

### Force layer

**Order of Battle**:
One player's Crusade force: every unit they can bring to a Battle, with its Supply Limit and Requisition Points.
_Avoid_: crusade (on its own), army, roster (a roster is the exported file a force was imported from)

**Crusade Card**:
One unit on an Order of Battle, with its experience, rank, Battle Honours, Battle Scars and combat tallies.
_Avoid_: unit card, crusade unit

**Crusade Army**:
The subset of an Order of Battle a player brings to one Battle, picked at the Muster. Only its Crusade Cards gain experience from that Battle.
_Avoid_: army list, battle roster, the list

**Muster**:
The pre-battle step where each player picks their Crusade Army from their Order of Battle, within the Battle's points limit.
_Avoid_: deployment, list building

**Post-battle Sequence**:
The steps run once after a Battle: record the result, award experience, test destroyed units, apply scars, rank up, gain a Requisition Point.
_Avoid_: aftermath, debrief, wrap-up

**Out of Action Test**:
The roll made for each Crusade Card destroyed in a Battle. A pass spares the unit a penalty; a fail costs a Battle Honour or adds a Battle Scar.
_Avoid_: injury roll, casualty roll

**Battle Honour**:
A permanent upgrade a Crusade Card earns on rank-up. One of Battle Trait, Weapon Modification or Crusade Relic.
_Avoid_: honour, trait (alone), upgrade

**Battle Scar**:
A permanent penalty a Crusade Card takes after failing an Out of Action test.
_Avoid_: scar, injury

**Attrition**:
The models and wounds a Crusade Card has lost and carries into its next Battle. A house rule; the core rules restore every unit.
_Avoid_: damage (alone), casualties (that is the destroyed-units step)

**Rank**:
A Crusade Card's experience tier: Battle-ready, Blooded, Battle-hardened, Heroic, Legendary.
_Avoid_: level

**Supply Limit**:
The points ceiling of an Order of Battle. Supply Used is the sum of its Crusade Cards' points.
_Avoid_: points limit, cap

**Requisition Point**:
Campaign currency an Order of Battle spends on Requisitions such as Increase Supply Limit or Fresh Recruits. Never more than ten.
_Avoid_: RP (in prose), resource

**Crusade Points**:
The score of a Crusade Card's accumulated upgrades: plus one per Battle Honour (two if Titanic, more for relic tiers), minus one per Battle Scar. Derived, never typed.
_Avoid_: CP (that is Command Points at the table)

**Game Master**:
The one person who operates the app and edits every Order of Battle and the Campaign Map. There is no per-player login.
_Avoid_: admin, owner, host

### Table layer

**Wound Calculator**:
The at-table tool that shows the roll needed at each step of the attack sequence: hit, wound, save. It states rolls, never predicted outcomes.
_Avoid_: damage calculator, simulator, predictor

**Modifier**:
Any rule that changes a roll needed: a weapon keyword, a unit ability, cover, a situation toggle, or a Battle Honour.
_Avoid_: buff, debuff, bonus

**Automatic Modifier**:
A Modifier the Wound Calculator applies by itself from the datasheet, shown with an "auto" marker and a one-tap override.
_Avoid_: auto-detect, inferred modifier

**Situation Toggle**:
A fact only the players know, so the Wound Calculator asks for it: remained stationary, charged this turn, target not visible, in cover, firing while in engagement range.
_Avoid_: manual modifier, flag

**Benefit of Cover**:
The +1 to an armour save a unit gets against ranged attacks while in cover, subject to the 10th edition exceptions.
_Avoid_: cover bonus, cover save
