using System;
using System.Globalization;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// Battle state as structure-of-arrays (plan, section 3.2; tech notes, section 2). Units and regiments live in
    /// fixed-capacity slots; a slot's index is stable for the unit's life, so systems loop over indices 0 to
    /// <see cref="EntityPool.HighWater"/>, skip dead slots, and split that range into disjoint chunks for the parallel
    /// runner. Systems read and write the arrays directly; spawning and despawning go through this class so the
    /// regiment counts and the pools stay consistent.
    /// <para>
    /// Freed slots are reset to zero, so a dead slot holds the same bits however it died; a snapshot can copy
    /// whole arrays and a restored world matches the original slot for slot, not only in the hash.
    /// </para>
    /// <para>
    /// Spawning and despawning change shared counters and the pools' free stacks, whose order decides future
    /// ids, so they run on one thread in a fixed order (ascending slot index), never inside parallel chunks.
    /// Systems that read neighbours while writing their own chunk write to scratch arrays and commit afterwards.
    /// </para>
    /// </summary>
    public sealed class World
    {
        /// <summary>
        /// The world fields (water, wetness, mud, …), or null in a world built without them (tests of units alone).
        /// Hashed and snapshotted with the rest of the world.
        /// </summary>
        public FieldStore? Fields { get; private set; }

        /// <summary>The state of the plan's casts (<see cref="CastSystem"/>), or null in a world without a plan.</summary>
        public CastStates? Casts { get; private set; }

        /// <summary>The battle's cause log (<see cref="CauseLog"/>), or null in a world built without one.</summary>
        public CauseLog? Causes { get; private set; }

        /// <summary>The decks' state (spec S2 section 8), or null in a battle without bridges or ice.</summary>
        public StructureStates? Structures { get; private set; }

        /// <summary>Gives the world its structures' state; once, before the first tick.</summary>
        public void AttachStructures(StructureStates structures)
        {
            if (Structures != null)
            {
                throw new InvalidOperationException("The world already has its structures.");
            }
            Structures = structures ?? throw new ArgumentNullException(nameof(structures));
        }

        /// <summary>Gives the world its cause log; once, before the first tick.</summary>
        public void AttachCauses(CauseLog causes)
        {
            if (Causes != null)
            {
                throw new InvalidOperationException("The world already has its cause log.");
            }
            Causes = causes ?? throw new ArgumentNullException(nameof(causes));
        }

        /// <summary>Swaps in another plan's cast states (<see cref="Timeline"/> rebasing a keyframe onto an edited plan).</summary>
        internal void ReplaceCasts(CastStates casts) =>
            Casts = casts ?? throw new ArgumentNullException(nameof(casts));

        /// <summary>Gives the world its cast states; once, before the first tick.</summary>
        public void AttachCasts(CastStates casts)
        {
            if (Casts != null)
            {
                throw new InvalidOperationException("The world already has its casts.");
            }
            Casts = casts ?? throw new ArgumentNullException(nameof(casts));
        }

        /// <summary>Gives the world its fields; once, before the first tick.</summary>
        public void AttachFields(FieldStore fields)
        {
            if (Fields != null)
            {
                throw new InvalidOperationException("The world already has its fields.");
            }
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }

        /// <summary>An empty world of the same capacities and field layout, for restoring a snapshot into.</summary>
        public World CreateBlank()
        {
            var blank = new World(Units.Capacity, Regiments.Capacity);
            if (Fields != null)
            {
                blank.AttachFields(new FieldStore(Fields.Rules, Fields.Columns, Fields.Rows));
            }
            if (Casts != null)
            {
                blank.AttachCasts(new CastStates(Casts.Count, Casts.Carry.Length));
            }
            if (Structures != null)
            {
                blank.AttachStructures(Structures.CreateBlank());
            }
            if (Causes != null)
            {
                blank.AttachCauses(
                    new CauseLog(Causes.TileTag.Length, Regiments.Capacity, Units.Capacity)
                );
            }
            return blank;
        }

        public World(int unitCapacity, int regimentCapacity)
        {
            Units = new EntityPool(unitCapacity);
            Regiments = new EntityPool(regimentCapacity);

            Position = new FixVec2[unitCapacity];
            Velocity = new FixVec2[unitCapacity];
            UnitType = new int[unitCapacity];
            UnitRegiment = new int[unitCapacity];
            Health = new int[unitCapacity];
            Morale = new int[unitCapacity];
            Flags = new UnitFlags[unitCapacity];
            FormationSlot = new int[unitCapacity];
            HazardTimer = new int[unitCapacity];

            RegimentSide = new Side[regimentCapacity];
            RegimentUnitType = new int[regimentCapacity];
            RegimentStartingSize = new int[regimentCapacity];
            RegimentAlive = new int[regimentCapacity];
            RegimentMorale = new int[regimentCapacity];
            RegimentAnchor = new int[regimentCapacity];
            RegimentFacing = new Angle[regimentCapacity];
            RegimentFiles = new int[regimentCapacity];
            Army = new ArmyState(regimentCapacity);
            for (int r = 0; r < regimentCapacity; r++)
            {
                RegimentAnchor[r] = -1;
            }
        }

        /// <summary>The bound on ground edits a snapshot of a world without fields may hold (the spatial hash's cell cap).</summary>
        private const int MaxGroundCells = 16_000_000;

        public EntityPool Units { get; }
        public EntityPool Regiments { get; }

        // Per unit, indexed by unit slot.

        /// <summary>Metres on the ground plane.</summary>
        public FixVec2[] Position { get; }

        /// <summary>Metres per tick.</summary>
        public FixVec2[] Velocity { get; }

        /// <summary>Index into the ruleset's unit types.</summary>
        public int[] UnitType { get; }

        /// <summary>The regiment's slot index.</summary>
        public int[] UnitRegiment { get; }

        /// <summary>Hit points in the ruleset's units; a unit at 0 or below is despawned by the hazards and combat systems.</summary>
        public int[] Health { get; }

        /// <summary>Per-soldier morale in the ruleset's units (regiments carry their own, below).</summary>
        public int[] Morale { get; }

        public UnitFlags[] Flags { get; }

        /// <summary>
        /// The soldier's place in its regiment's formation, 0 for the officer or standard-bearer: soldiers are numbered
        /// in the order they joined, and slot k ≥ 1 stands in file (k - 1) mod files, rank (k - 1) / files behind
        /// the officer (<see cref="Formation"/>).
        /// </summary>
        public int[] FormationSlot { get; }

        /// <summary>
        /// Ticks left in the soldier's hazard state, which its flags name (a soldier is in at most one): until it
        /// drowns while <see cref="UnitFlags.Swept"/>, until it gets up while <see cref="UnitFlags.Stunned"/>, until the
        /// crowd pushes it over while <see cref="UnitFlags.Teetering"/>. 0 otherwise.
        /// </summary>
        public int[] HazardTimer { get; }

        /// <summary>The ground as soldiers changed it this battle (dug and filled cells, spec S2 section 14).</summary>
        public GroundEdits Ground { get; } = new GroundEdits();

        /// <summary>Per-regiment orders, volleys and morale bookkeeping (spec S2, sections 5 to 7).</summary>
        public ArmyState Army { get; }

        /// <summary>Hazard events so far in this battle; the cause log (M3 item 9) will say who and why.</summary>
        public BattleCounters Counters { get; } = new BattleCounters();

        // Per regiment, indexed by regiment slot.

        public Side[] RegimentSide { get; }

        public int[] RegimentUnitType { get; }

        /// <summary>Soldiers spawned into the regiment; the "below about 10% counts as wiped" rule measures against it.</summary>
        public int[] RegimentStartingSize { get; }

        public int[] RegimentAlive { get; }

        public int[] RegimentMorale { get; }

        /// <summary>
        /// The unit slot of the officer or standard-bearer the formation forms on, or -1 when it has fallen and no one
        /// has taken the standard yet (the movement step promotes the soldier with the lowest formation slot).
        /// </summary>
        public int[] RegimentAnchor { get; }

        /// <summary>Which way the formation faces; the officer's heading turns it, at a limited rate.</summary>
        public Angle[] RegimentFacing { get; }

        /// <summary>How many soldiers stand side by side in a rank.</summary>
        public int[] RegimentFiles { get; }

        public EntityId AddRegiment(
            Side side,
            int unitType,
            int morale,
            int files = 1,
            Angle facing = default
        )
        {
            if (files < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(files));
            }
            EntityId id = Regiments.Allocate();
            int r = id.Index;
            RegimentSide[r] = side;
            RegimentUnitType[r] = unitType;
            RegimentStartingSize[r] = 0;
            RegimentAlive[r] = 0;
            RegimentMorale[r] = morale;
            RegimentAnchor[r] = -1;
            RegimentFacing[r] = facing;
            RegimentFiles[r] = files;
            Army.Clear(r);
            return id;
        }

        /// <summary>
        /// A soldier of the regiment's unit type, standing still, with no flags, in the next formation slot; the
        /// regiment's first soldier is its officer and anchor.
        /// </summary>
        public EntityId SpawnUnit(EntityId regiment, FixVec2 position, int health, int morale)
        {
            if (!Regiments.IsAlive(regiment))
            {
                throw new InvalidOperationException("Regiment " + regiment + " is not alive.");
            }
            EntityId id = Units.Allocate();
            int u = id.Index;
            int r = regiment.Index;
            Position[u] = position;
            Velocity[u] = FixVec2.Zero;
            UnitType[u] = RegimentUnitType[r];
            UnitRegiment[u] = r;
            Health[u] = health;
            Morale[u] = morale;
            Flags[u] = UnitFlags.None;
            HazardTimer[u] = 0;
            FormationSlot[u] = RegimentStartingSize[r];
            if (RegimentStartingSize[r] == 0)
            {
                RegimentAnchor[r] = u;
            }
            RegimentStartingSize[r]++;
            RegimentAlive[r]++;
            return id;
        }

        public void DespawnUnit(EntityId unit)
        {
            if (!Units.IsAlive(unit))
            {
                throw new InvalidOperationException("Unit " + unit + " is not alive.");
            }
            int u = unit.Index;
            RegimentAlive[UnitRegiment[u]]--;
            if (RegimentAnchor[UnitRegiment[u]] == u)
            {
                RegimentAnchor[UnitRegiment[u]] = -1;
            }
            FormationSlot[u] = 0;
            Position[u] = FixVec2.Zero;
            Velocity[u] = FixVec2.Zero;
            UnitType[u] = 0;
            UnitRegiment[u] = 0;
            Health[u] = 0;
            Morale[u] = 0;
            Flags[u] = UnitFlags.None;
            HazardTimer[u] = 0;
            if (Causes != null)
            {
                // A reused slot must never inherit this soldier's links.
                Causes.UnitSwept[u] = CauseLog.None;
                Causes.UnitPushed[u] = CauseLog.None;
            }
            Units.Free(unit);
        }

        /// <summary>Removes an empty regiment. Regiments with soldiers left must lose them first.</summary>
        public void RemoveRegiment(EntityId regiment)
        {
            if (!Regiments.IsAlive(regiment))
            {
                throw new InvalidOperationException("Regiment " + regiment + " is not alive.");
            }
            if (RegimentAlive[regiment.Index] != 0)
            {
                throw new InvalidOperationException(
                    "Regiment "
                        + regiment
                        + " still has "
                        + RegimentAlive[regiment.Index].ToString(CultureInfo.InvariantCulture)
                        + " soldiers."
                );
            }
            int r = regiment.Index;
            RegimentSide[r] = Side.Attacker;
            RegimentUnitType[r] = 0;
            RegimentStartingSize[r] = 0;
            RegimentMorale[r] = 0;
            RegimentAnchor[r] = -1;
            RegimentFacing[r] = Angle.Zero;
            RegimentFiles[r] = 0;
            Army.Clear(r);
            if (Causes != null)
            {
                Causes.RegimentSwept[r] = CauseLog.None;
                Causes.RegimentSweptTick[r] = 0;
            }
            Regiments.Free(regiment);
        }

        /// <summary>
        /// Every slot of every array, up to capacity, in a fixed order: a restored world matches the original slot for
        /// slot, dead slots included (they are zero, see the class notes).
        /// </summary>
        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteByte(Fields == null ? (byte)0 : (byte)1);
            Fields?.WriteTo(writer);
            Units.WriteTo(writer);
            Regiments.WriteTo(writer);
            int units = Units.Capacity;
            for (int u = 0; u < units; u++)
            {
                writer.WriteInt64(Position[u].X.Raw);
                writer.WriteInt64(Position[u].Y.Raw);
                writer.WriteInt64(Velocity[u].X.Raw);
                writer.WriteInt64(Velocity[u].Y.Raw);
                writer.WriteInt32(UnitType[u]);
                writer.WriteInt32(UnitRegiment[u]);
                writer.WriteInt32(Health[u]);
                writer.WriteInt32(Morale[u]);
                writer.WriteInt32(unchecked((int)Flags[u]));
                writer.WriteInt32(FormationSlot[u]);
                writer.WriteInt32(HazardTimer[u]);
            }
            for (int r = 0; r < Regiments.Capacity; r++)
            {
                writer.WriteByte((byte)RegimentSide[r]);
                writer.WriteInt32(RegimentUnitType[r]);
                writer.WriteInt32(RegimentStartingSize[r]);
                writer.WriteInt32(RegimentAlive[r]);
                writer.WriteInt32(RegimentMorale[r]);
                writer.WriteInt32(RegimentAnchor[r]);
                writer.WriteInt32(RegimentFacing[r].Units);
                writer.WriteInt32(RegimentFiles[r]);
            }
            Counters.WriteTo(writer);
            Army.WriteTo(writer);
            Ground.WriteTo(writer);
            writer.WriteByte(Structures == null ? (byte)0 : (byte)1);
            Structures?.WriteTo(writer);
            writer.WriteByte(Casts == null ? (byte)0 : (byte)1);
            Casts?.WriteTo(writer);
            writer.WriteByte(Causes == null ? (byte)0 : (byte)1);
            Causes?.WriteTo(writer);
        }

        /// <summary>
        /// Throws <see cref="FormatException"/> when the world breaks its own rules: live units in dead regiments,
        /// regiment counts that don't match their soldiers, dead slots that aren't zero, unknown flag bits.
        /// </summary>
        internal void CheckInvariants()
        {
            Units.CheckInvariants();
            Regiments.CheckInvariants();
            const UnitFlags known =
                UnitFlags.Launched
                | UnitFlags.Stunned
                | UnitFlags.Teetering
                | UnitFlags.Swept
                | UnitFlags.Burning
                | UnitFlags.Routing
                | UnitFlags.Garrisoned;
            var counted = new int[Regiments.Capacity];
            for (int u = 0; u < Units.Capacity; u++)
            {
                if (Units.IsAlive(u))
                {
                    int r = UnitRegiment[u];
                    if (r < 0 || r >= Regiments.Capacity || !Regiments.IsAlive(r))
                    {
                        throw new FormatException(
                            "A live unit belongs to a regiment that is not alive."
                        );
                    }
                    if ((Flags[u] & ~known) != 0)
                    {
                        throw new FormatException("A unit has unknown flag bits.");
                    }
                    var hazard =
                        Flags[u] & (UnitFlags.Swept | UnitFlags.Stunned | UnitFlags.Teetering);
                    if (
                        HazardTimer[u] < 0
                        || (hazard & (hazard - 1)) != 0
                        || (hazard == UnitFlags.None && HazardTimer[u] != 0)
                        || (
                            hazard != UnitFlags.None
                            && HazardTimer[u] == 0
                            && hazard != UnitFlags.Swept
                        )
                    )
                    {
                        throw new FormatException("A unit's hazard state is invalid.");
                    }
                    if (FormationSlot[u] < 0 || FormationSlot[u] >= RegimentStartingSize[r])
                    {
                        throw new FormatException(
                            "A unit's formation slot is outside its regiment."
                        );
                    }
                    counted[r]++;
                }
                else if (
                    Position[u] != FixVec2.Zero
                    || Velocity[u] != FixVec2.Zero
                    || UnitType[u] != 0
                    || UnitRegiment[u] != 0
                    || Health[u] != 0
                    || Morale[u] != 0
                    || Flags[u] != UnitFlags.None
                    || FormationSlot[u] != 0
                    || HazardTimer[u] != 0
                )
                {
                    throw new FormatException("A dead unit slot is not zero.");
                }
            }
            // Formation slots are unique within a regiment: mark each in a block per regiment.
            var slotStart = new long[Regiments.Capacity + 1];
            for (int r = 0; r < Regiments.Capacity; r++)
            {
                if (Regiments.IsAlive(r) && RegimentStartingSize[r] < 0)
                {
                    throw new FormatException("A regiment's starting size is negative.");
                }
                slotStart[r + 1] =
                    slotStart[r] + (Regiments.IsAlive(r) ? RegimentStartingSize[r] : 0);
            }
            if (slotStart[Regiments.Capacity] > (long)Units.Capacity * 16)
            {
                throw new FormatException("Regiment starting sizes are implausibly large.");
            }
            var slotTaken = new bool[slotStart[Regiments.Capacity]];
            for (int u = 0; u < Units.Capacity; u++)
            {
                if (Units.IsAlive(u))
                {
                    long at = slotStart[UnitRegiment[u]] + FormationSlot[u];
                    if (slotTaken[at])
                    {
                        throw new FormatException("Two soldiers share a formation slot.");
                    }
                    slotTaken[at] = true;
                }
            }
            for (int r = 0; r < Regiments.Capacity; r++)
            {
                if (Regiments.IsAlive(r))
                {
                    if (
                        RegimentAlive[r] != counted[r]
                        || RegimentStartingSize[r] < RegimentAlive[r]
                    )
                    {
                        throw new FormatException(
                            "A regiment's soldier counts do not match its units."
                        );
                    }
                    int anchor = RegimentAnchor[r];
                    if (
                        RegimentFiles[r] < 1
                        || RegimentFiles[r] > System.Math.Max(1, RegimentStartingSize[r])
                        || anchor < -1
                        || anchor >= Units.Capacity
                        || (anchor >= 0 && (!Units.IsAlive(anchor) || UnitRegiment[anchor] != r))
                    )
                    {
                        throw new FormatException(
                            "A regiment's formation (files or anchor) is invalid."
                        );
                    }
                }
                else if (
                    RegimentSide[r] != Side.Attacker
                    || RegimentUnitType[r] != 0
                    || RegimentStartingSize[r] != 0
                    || RegimentAlive[r] != 0
                    || RegimentMorale[r] != 0
                    || RegimentAnchor[r] != -1
                    || RegimentFacing[r] != Angle.Zero
                    || RegimentFiles[r] != 0
                    || !Army.IsClear(r)
                )
                {
                    throw new FormatException("A dead regiment slot is not zero.");
                }
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            byte hasFields = reader.ReadByte();
            if (hasFields != (Fields == null ? 0 : 1))
            {
                throw new FormatException("The snapshot's fields don't match this world's.");
            }
            Fields?.ReadFrom(reader);
            Units.ReadFrom(reader);
            Regiments.ReadFrom(reader);
            for (int u = 0; u < Units.Capacity; u++)
            {
                Position[u] = new FixVec2(
                    Fix.FromRaw(reader.ReadInt64()),
                    Fix.FromRaw(reader.ReadInt64())
                );
                Velocity[u] = new FixVec2(
                    Fix.FromRaw(reader.ReadInt64()),
                    Fix.FromRaw(reader.ReadInt64())
                );
                UnitType[u] = reader.ReadInt32();
                UnitRegiment[u] = reader.ReadInt32();
                Health[u] = reader.ReadInt32();
                Morale[u] = reader.ReadInt32();
                Flags[u] = unchecked((UnitFlags)reader.ReadInt32());
                FormationSlot[u] = reader.ReadInt32();
                HazardTimer[u] = reader.ReadInt32();
            }
            for (int r = 0; r < Regiments.Capacity; r++)
            {
                byte side = reader.ReadByte();
                if (side > (byte)Side.Defender)
                {
                    throw new FormatException("Snapshot side is out of range.");
                }
                RegimentSide[r] = (Side)side;
                RegimentUnitType[r] = reader.ReadInt32();
                RegimentStartingSize[r] = reader.ReadInt32();
                RegimentAlive[r] = reader.ReadInt32();
                RegimentMorale[r] = reader.ReadInt32();
                RegimentAnchor[r] = reader.ReadInt32();
                int facing = reader.ReadInt32();
                if (facing < 0 || facing >= Angle.UnitsPerTurn)
                {
                    throw new FormatException("Snapshot facing is out of range.");
                }
                RegimentFacing[r] = Angle.FromUnits(facing);
                RegimentFiles[r] = reader.ReadInt32();
            }
            Counters.ReadFrom(reader);
            Army.ReadFrom(reader);
            Ground.ReadFrom(reader, Fields == null ? MaxGroundCells : Fields.Columns * Fields.Rows);
            if (reader.ReadByte() != (Structures == null ? 0 : 1))
            {
                throw new FormatException("The snapshot's structures don't match this world's.");
            }
            Structures?.ReadFrom(
                reader,
                Fields == null ? MaxGroundCells : Fields.Columns * Fields.Rows
            );
            if (reader.ReadByte() != (Casts == null ? 0 : 1))
            {
                throw new FormatException("The snapshot's casts don't match this world's.");
            }
            Casts?.ReadFrom(reader, Regiments.Capacity);
            if (reader.ReadByte() != (Causes == null ? 0 : 1))
            {
                throw new FormatException("The snapshot's cause log doesn't match this world's.");
            }
            Causes?.ReadFrom(reader);
        }

        /// <summary>
        /// Every piece of state that can affect the future, in a fixed order: pools first, then each live slot's
        /// fields. Dead slots are skipped, since a spawn overwrites every field.
        /// </summary>
        public void AppendTo(SimHash hash)
        {
            Units.AppendTo(hash);
            Regiments.AppendTo(hash);
            Fields?.AppendTo(hash);
            for (int u = 0; u < Units.HighWater; u++)
            {
                if (!Units.IsAlive(u))
                {
                    continue;
                }
                hash.AppendInt64(Position[u].X.Raw);
                hash.AppendInt64(Position[u].Y.Raw);
                hash.AppendInt64(Velocity[u].X.Raw);
                hash.AppendInt64(Velocity[u].Y.Raw);
                hash.AppendInt32(UnitType[u]);
                hash.AppendInt32(UnitRegiment[u]);
                hash.AppendInt32(Health[u]);
                hash.AppendInt32(Morale[u]);
                hash.AppendInt32(unchecked((int)Flags[u]));
                hash.AppendInt32(FormationSlot[u]);
                hash.AppendInt32(HazardTimer[u]);
            }
            for (int r = 0; r < Regiments.HighWater; r++)
            {
                if (!Regiments.IsAlive(r))
                {
                    continue;
                }
                hash.AppendInt32((int)RegimentSide[r]);
                hash.AppendInt32(RegimentUnitType[r]);
                hash.AppendInt32(RegimentStartingSize[r]);
                hash.AppendInt32(RegimentAlive[r]);
                hash.AppendInt32(RegimentMorale[r]);
                hash.AppendInt32(RegimentAnchor[r]);
                hash.AppendInt32(RegimentFacing[r].Units);
                hash.AppendInt32(RegimentFiles[r]);
            }
            Counters.AppendTo(hash);
            Army.AppendTo(hash, Regiments);
            Ground.AppendTo(hash);
            Structures?.AppendTo(hash);
            Casts?.AppendTo(hash);
            Causes?.AppendTo(hash);
        }
    }
}
