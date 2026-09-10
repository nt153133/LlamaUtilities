using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Buddy.Coroutines;
using Clio.XmlEngine;
using ff14bot;
using ff14bot.Enums;
using ff14bot.Managers;
using ff14bot.Navigation;
using TreeSharp;

namespace LlamaUtilities.OrderbotTags
{
    /// <summary>Assigns a captured Beastmaster pet by bestiary ID and optionally summons its Battlehorn.</summary>
    /// <remarks>
    /// Requires RB 1.0.913 or later. Uses the native slot API instead of localized bestiary callbacks.
    /// Assignment can dismiss the current familiar and moves a pet out of any previous slot.
    /// Runs exclusively on the OrderBot coroutine; failure stops the profile without marking success.
    /// </remarks>
    [XmlElement("SetBeastmasterPet")]
    public class SetBeastmasterPet : LLProfileBehavior
    {
        // Global 7.56 player actions: First, Second, Third Battlehorn. Cast on self, not the pet.
        // Each horn action targets the player.
        private static readonly uint[] HornActions = { 44881, 44892, 44894 };
        // Allow mount and familiar arrival animations to settle before another request.
        private const int TransitionDelayMilliseconds = 1000;
        // Tolerate recast rounding and server latency without waiting indefinitely.
        private const int CooldownGraceMilliseconds = 5000;

        private bool _isDone;

        /// <summary>Gets or sets the positive Beastmaster bestiary ID (Cu Sith is 1), not an NPC or action ID.</summary>
        [XmlAttribute("PetId")]
        [XmlAttribute("PetID")]
        public int PetId { get; set; }

        /// <summary>Gets or sets the destination Battlehorn, numbered 1 through 3; defaults to the first.</summary>
        [XmlAttribute("Battlehorn")]
        [DefaultValue(1)]
        public int Battlehorn { get; set; } = 1;

        /// <summary>Gets or sets whether to summon after assignment; false assigns only and is the default.</summary>
        [XmlAttribute("Summon")]
        [DefaultValue(false)]
        public bool Summon { get; set; }

        /// <summary>Gets or sets the maximum wait for each server/cast transition in milliseconds; defaults to 10000.</summary>
        /// <remarks>Normal Battlehorn cooldown recovery is timed separately from the observed remaining cooldown.</remarks>
        [XmlAttribute("Timeout")]
        [DefaultValue(10000)]
        public int Timeout { get; set; } = 10000;

        /// <summary>Gets whether the requested assignment and optional summon have both been verified.</summary>
        public override bool IsDone => _isDone;

        /// <summary>Gets true to serialize this setup operation ahead of ordinary profile activity.</summary>
        public override bool HighPriority => true;

        /// <summary>Creates a tag with the same defaults for XML and programmatic callers.</summary>
        public SetBeastmasterPet() { }

        /// <summary>Clears completion when OrderBot repeats or resets the tag.</summary>
        protected override void OnResetCachedDone() => _isDone = false;

        /// <summary>Creates the coroutine-driven behavior; no external tasks or frame locks span awaits.</summary>
        /// <returns>The OrderBot action which performs and verifies setup.</returns>
        protected override Composite CreateBehavior() => new ActionRunCoroutine(_ => Execute());

        private async Task Execute()
        {
            if (_isDone)
                return;

            // The enum is byte-backed: validate before conversion so e.g. 257 cannot become Cu Sith.
            if (PetId <= 0 || PetId > byte.MaxValue || !Enum.IsDefined(typeof(BeastmasterPet), (BeastmasterPet)PetId))
            {
                Fail($"PetId {PetId} is not a nonempty Beastmaster bestiary ID.");
                return;
            }
            if (Battlehorn < 1 || Battlehorn > HornActions.Length || Timeout <= 0)
            {
                Fail("Battlehorn must be 1, 2, or 3 and Timeout must be positive.");
                return;
            }
            if (Core.Me.CurrentJob != ClassJobType.BeastMaster)
            {
                Fail("Change to Beastmaster before assigning a Battlehorn.");
                return;
            }

            var pet = (BeastmasterPet)PetId;
            var action = HornActions[Battlehorn - 1];
            if (!ActionManager.HasSpell(action))
            {
                Fail($"Battlehorn {Battlehorn} is not unlocked at the current level.");
                return;
            }

            if (!await EnsurePetUnlocked(pet) || !await WaitForCurrentCast() || !await AssignPet(pet))
                return;

            if (Summon && !await SummonPet(pet, action))
                return;

            Log.Information($"Pet {PetId} ({pet}) assigned to Battlehorn {Battlehorn}" + (Summon ? " and summoned." : "."));
            _isDone = true;
        }

        private async Task<bool> EnsurePetUnlocked(BeastmasterPet pet)
        {
            // RB's unlock list stays empty until the bestiary requests the login mask. Open it
            // only when needed, and close only the window this tag opened; never toggle an open book.
            var openedBook = false;
            if (PetManager.UnlockedBeastmasterPets.Count == 0 &&
                RaptureAtkUnitManager.GetWindowByName("XBMMonsterNotebook") == null)
            {
                ChatManager.SendChat("/bestiary");
                openedBook = true;
            }
            var unlocked = await Coroutine.Wait(Timeout, () => PetManager.IsBeastmasterPetUnlocked(pet));
            if (openedBook)
            {
                var book = RaptureAtkUnitManager.GetWindowByName("XBMMonsterNotebook");
                // Standard addon close callback; all assignment/readback still uses PetManager.
                book?.SendAction(1, 3, 0xFFFFFFFF);
                await Coroutine.Wait(Timeout, () => RaptureAtkUnitManager.GetWindowByName("XBMMonsterNotebook") == null);
            }
            if (!unlocked)
            {
                Fail($"Pet {PetId} ({pet}) is not reported unlocked. Capture it or open the bestiary to load its unlock data.");
                return false;
            }

            return true;
        }

        private async Task<bool> WaitForCurrentCast()
        {
            // A routine can start a horn while OrderBot advances through a preceding While.
            // Let that cast and its familiar arrival settle before snapshotting the old pet
            // or changing slots, otherwise the arriving pet races assignment dismissal.
            if (Core.Me.IsCasting)
            {
                Log.Information("Waiting for the current cast before assigning a Battlehorn.");
                if (!await Coroutine.Wait(Timeout, () => !Core.Me.IsCasting))
                {
                    Fail("The current cast did not finish before Battlehorn assignment.");
                    return false;
                }
                await Coroutine.Sleep(TransitionDelayMilliseconds);
            }

            return true;
        }

        private async Task<bool> AssignPet(BeastmasterPet pet)
        {
            if (IsAssigned(pet))
                return true;

            var previousPet = Core.Me.Pet;
            var previousPetId = previousPet != null && previousPet.IsValid ? previousPet.ObjectId : GameObjectManager.EmptyGameObject;
            // XML horns are 1-based; RB's setter/array are 0-based. True only means request sent.
            if (!PetManager.SetBeastmasterPetSlot(Battlehorn - 1, pet) ||
                !await Coroutine.Wait(Timeout, () => IsAssigned(pet)))
            {
                Fail($"Could not verify pet {PetId} in Battlehorn {Battlehorn}; the client may be occupied or in combat.");
                return false;
            }
            // Reassignment dismisses the existing familiar asynchronously. A still-visible old
            // pet with the same gauge horn must not count as the newly assigned species.
            if (Summon && previousPetId != GameObjectManager.EmptyGameObject &&
                !await Coroutine.Wait(Timeout, () => Core.Me.Pet == null || !Core.Me.Pet.IsValid || Core.Me.Pet.ObjectId != previousPetId))
            {
                Fail("Assignment succeeded, but the previous familiar has not departed.");
                return false;
            }

            return true;
        }

        private async Task<bool> SummonPet(BeastmasterPet pet, uint action)
        {
            if (IsSummoned(pet))
                return true;

            // Horns have a cast time; movement cancels them. Do not leave duties or change targets.
            Navigator.PlayerMover.MoveStop();
            if (Core.Me.IsMounted)
            {
                ActionManager.Dismount();
                // Arrival from GetTo may still be completing the mount transition. Give
                // dismount its own timeout, then a short animation/server settling interval;
                // neither consumes the subsequent horn cooldown/readiness wait.
                if (!await Coroutine.Wait(Timeout, () => !Core.Me.IsMounted))
                {
                    Fail("Could not dismount before summoning the Battlehorn.");
                    return false;
                }
                await Coroutine.Sleep(TransitionDelayMilliseconds);
                Navigator.PlayerMover.MoveStop();
            }

            // RB 1.0.913 CanCast rejects usable horn actions. Check state and cooldown here;
            // the familiar readback below remains the success criterion.
            if (!await Coroutine.Wait(Timeout, () => !Core.Me.IsMounted && !Core.Me.IsCasting))
            {
                Fail($"Battlehorn {Battlehorn} is blocked by a mount or active cast.");
                return false;
            }
            var remaining = ReadCooldown(action);
            if (remaining > TimeSpan.Zero)
            {
                // A retreat can impose a full 90-second cooldown. It is normal game state,
                // not a failed server transition: budget the observed remainder plus five
                // seconds for tick/server lag, independently of the profile's Timeout.
                Log.Information($"Battlehorn {Battlehorn} cooldown: {remaining.TotalSeconds:F1}s remaining; waiting for recovery.");
                if (!await Coroutine.Wait(CooldownWaitMilliseconds(remaining), () => ReadCooldown(action) <= TimeSpan.Zero))
                {
                    Fail($"Battlehorn {Battlehorn} cooldown did not recover as expected; {ReadCooldown(action).TotalSeconds:F1}s remain.");
                    return false;
                }
                Log.Information($"Battlehorn {Battlehorn} cooldown recovered.");
            }
            if (!await Coroutine.Wait(Timeout, () => !Core.Me.IsMounted && !Core.Me.IsCasting))
            {
                Fail($"Battlehorn {Battlehorn} is still blocked by a mount or active cast after cooldown recovery.");
                return false;
            }
            if (!IsAssigned(pet))
            {
                Fail($"Battlehorn {Battlehorn}'s assignment changed before summoning pet {PetId}.");
                return false;
            }
            // Dismounting can trigger an automatic resummon; avoid casting again if it finished.
            if (!IsSummoned(pet))
            {
                if (!ActionManager.DoAction(action, Core.Me))
                {
                    Fail($"RB rejected the summon request for Battlehorn {Battlehorn} (action {action}).");
                    return false;
                }
                Log.Information($"Summon requested for Battlehorn {Battlehorn} (action {action}); waiting for pet {PetId}.");
                if (!await Coroutine.Wait(Timeout, () => IsSummoned(pet)))
                {
                    Fail($"Battlehorn {Battlehorn} summon was requested, but pet {PetId} did not appear before the timeout.");
                    return false;
                }
            }

            return true;
        }

        // Re-read recast memory on every coroutine poll; do not retain a cooldown snapshot.
        private static TimeSpan ReadCooldown(uint action)
        {
            using (Core.Memory.AcquireFrame())
            using (Core.Memory.TemporaryCacheState(false))
                return DataManager.GetSpellData(action).Cooldown;
        }

        // Clamp the millisecond conversion to the coroutine API's integer range. The five-second
        // buffer absorbs recast rounding and server/tick latency without extending waits forever.
        private static int CooldownWaitMilliseconds(TimeSpan remaining) =>
            (int)Math.Min(int.MaxValue, Math.Ceiling(Math.Max(0, remaining.TotalMilliseconds)) + CooldownGraceMilliseconds);

        private bool IsAssigned(BeastmasterPet pet)
        {
            using (Core.Memory.AcquireFrame())
            using (Core.Memory.TemporaryCacheState(false))
            {
                var slots = PetManager.BeastmasterPetSlots;
                return slots.Length == PetManager.BeastmasterPetSlotCount && slots[Battlehorn - 1] == pet;
            }
        }

        private bool IsSummoned(BeastmasterPet pet)
        {
            using (Core.Memory.AcquireFrame())
            using (Core.Memory.TemporaryCacheState(false))
            {
                // As in Magitek, the gauge uses horns 1-3. Require a valid pet as well so a
                // stale gauge/assignment alone cannot count as a successful summon.
                var familiar = Core.Me.Pet;
                return IsAssigned(pet) && ActionResourceManager.BeastMaster.ActiveBattlehorn == Battlehorn &&
                    familiar != null && familiar.IsValid;
            }
        }

        private void Fail(string message)
        {
            Log.Error(message);
            // A quest must not continue as though setup succeeded. Reset/restart permits a retry.
            TreeRoot.Stop("SetBeastmasterPet: " + message);
        }
    }
}