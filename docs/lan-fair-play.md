# Fair play in LAN battles

A LAN battle is **host-authoritative**. The host's game runs the whole match: both decks, the shuffle, every rule. The guest's game shows what the host sends it and sends its moves back. The guest only ever receives what it may see (`MatchEvent.For`), and an unmodified host never shows its player the guest's hand. Still, the host's game holds everything: the guest's hand, both decks' order and the random seed.

This page covers what the game checks today, what it cannot check, and the larger design (**private decks**) that would close the remaining gap.

## What the game does today

### A shared seed

Every battle's shuffle comes from both devices, so neither can choose it, nor try seeds until one suits it (`FairPlay`, `LanSession`):

1. When the battle is about to begin (Start the battle, or a rematch both players asked for), the host picks a random 16-byte part and sends only its SHA-256 hash, a **promise**.
2. The guest picks its own random part and sends it in the clear.
3. The host begins the battle with the seed `SHA-256(host part ‖ guest part)` and sends its part with the battle's start.
4. The guest checks that the host's part matches the promise and computes the same seed.

The host is bound to its part before it sees the guest's. The guest chooses its part without knowing the host's. If the host sends a different promise before the battle begins (trying again for a better seed), or a part that breaks its promise, the guest leaves and says why.

### The record, checked at the end

Once a battle is over, the host sends the guest the battle's **record**: both decks, the seed and every move. The guest replays it with its own copy of the rules (`FairPlay.Check`) and checks that:

- the seed is the one both devices made;
- its deck is exactly the one it brought;
- the host's deck has the faction and leader the host showed, and follows the deck rules;
- every move in the record is legal (the replay accepts each one) and the record reaches the battle's end;
- every event its screen was shown matches the replay byte for byte (cards, rows, scores, round results);
- its own moves in the record are exactly the ones it made, in order. Three exceptions are allowed: a move it was unsure had arrived (sent just as the link dropped, or unanswered when the battle ended) may be missing; turns it ran out of time on (the host's clock); and a final give-up from the host claiming the win while the guest was away.

The result shows on the guest's end screen: "Fair play checked" in muted text, or in red, what didn't add up. A guest that resumed a battle after its game closed checks the seed (kept in its save) but not its earlier moves, which it no longer remembers.

## What it cannot check

- **Peeking.** A tampered host can still read the guest's hand and the order of both decks during the battle. Nothing the guest sees afterwards reveals that it happened.
- **The clock.** Timeouts are the host's call. A tampered host could time the guest out early (the guest would notice its fuse never ran out). The same goes for claiming the win while the guest is away.
- **A host that quits.** The host learns the seed first, once the guest's part arrives. It cannot change it, but it could drop the battle and host a new one. That is visible: the guest is back in the lobby, and has to join again.
- **The guest's honesty** needs no check: the host's game refuses any illegal move from the guest, and the guest never holds the host's hidden cards.

For battles between friends on one network, this covers what matters: a rigged shuffle, an altered result, or a move made for you shows up at the end of the battle.

## The larger design: private decks

To stop peeking as well, no device may ever hold the other's hidden cards. Each game keeps its own deck order and hand. The two run the battle in **lockstep** and reveal a card only when the rules make it public. This is "mental poker": card games played fairly without a trusted dealer.

### The pieces

**1. Committed decks.** Before the battle, each device shuffles its own deck with its own secret randomness. It then commits to the result: for each deck position, a hash of (card id, position, random salt), sent to the other device. The commitments together (or a Merkle root over them) bind the device to the whole order without revealing any of it.

**2. Reveals with proofs.** Drawing, playing or discarding a card reveals that position's card id and salt; the other device checks it against the commitment. Hands are committed the same way when dealt, so a card played "from the hand" can be proven to have been there.

**3. Shared randomness for public random effects.** Some effects need randomness neither side may control. Each one gets its own commit-reveal coin flip, the same two-step exchange as today's shared seed, keyed by the event's number:
- the coin toss for who goes first;
- the unit Monsters keep between rounds;
- Emhyr, Invader of the North's random medic picks;
- the three cards the Emperor of Nilfgaard reveals from the opponent's hand: a shared roll picks hand positions, and the owner reveals and proves those cards.

**4. Private effects stay private.** Effects that only touch a device's own hidden cards run on that device alone, and only the result is revealed: a spy's draws, Muster pulling copies from the deck, a medic reviving from the discard pile (public anyway), Eredin, Destroyer of Worlds' pick from the deck.

**5. Proving absence.** Some effects depend on a card *not* being somewhere. Muster pulls every copy from the deck, and "nothing left to play" passes for you. A position commitment can prove a card is present, but not cheaply that it is absent. The usual answer is to trust these during the battle and check them all at the end.

**6. The final reveal.** When the battle ends, both devices reveal every salt and their whole deck order. Each checks the other's commitments, then replays the full battle with all cards known, as the guest does today. Any lie (a card played that was never there, a Muster that skipped a copy, a false "nothing to play") is found at that point.

### What it would take

- **The rules engine.** `Match` currently needs every card. It would have to run with unknown cards on the other side: `CardIds.Hidden` already exists for display, but the rules themselves would need it. Each side's hidden zones are resolved by their owner, and commands carry the revealed cards and their proofs. Every ability that reaches into a hidden zone or uses randomness would need a two-party version, as above.
- **The protocol.** The protocol needs messages for commitments, reveals and coin flips. Lockstep execution has to survive reconnects and resumes without either side learning the other's secrets, and saves have to keep each side's salts.
- **Testing.** Each ability needs fuzzing on both sides plus tests of tampering, and the existing hidden-information views need re-verifying.

It is a large rebuild of the rules core, the network protocol and the saves: weeks of work and a long tail of edge cases in abilities. Its one benefit over today's checks is stopping a tampered host from peeking.

### Alternatives

- **A trusted server.** A dedicated server (not Unity Relay, which only forwards packets) would run the match, and both players would be guests. This is how online card games do it. Peeking becomes impossible for both players without any mental-poker cryptography, but it needs a hosted service, which LAN play does not have.
- **YGOPro's approach.** Each device receives a filtered view, exactly as this game does today. The host process still holds everything, so it gives the same protection as what we have.

## Where the code is

| Piece | File |
|---|---|
| Seed parts, promises, the record check | `GwentCardGame/Assets/Scripts/Rules/FairPlay.cs` |
| Seed exchange, sending and checking the record | `GwentCardGame/Assets/Scripts/Net/LanSession.cs` |
| The guest's seed kept for a resumed battle | `GwentCardGame/Assets/Scripts/Net/LanSave.cs` |
| The result on the end screen | `GwentCardGame/Assets/Scripts/Battle/EndScreen.cs` |
| Tests (honest battles, every kind of tampering) | `GwentCardGame/Assets/Tests/EditMode/FairPlayTests.cs` |
