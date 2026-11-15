using System;
using GodGame.Sim;

namespace GodGame.Bridge
{
    /// <summary>Snapshot of Lightning v0 strikes, falling/fallen trees, and active/extinguished fire patches (<see cref="StrikeStates"/>).</summary>
    public sealed class StrikeSnapshot
    {
        public int Tick;

        public int StrikeCount;
        public int[] StrikeXMm = new int[16];
        public int[] StrikeYMm = new int[16];
        public int[] StrikeTick = new int[16];
        public int[] StrikeEvent = new int[16];

        public int FallCount;
        public int[] FallTree = new int[16];
        public int[] FallHeadingUnits = new int[16];
        public int[] FallTick = new int[16];
        public int[] FallCause = new int[16];
        public bool[] FallLanded = new bool[16];

        public int FireCount;
        public int[] FireBlocker = new int[16];
        public int[] FireEnds = new int[16];
        public int[] FireEvent = new int[16];
        public bool[] FireOut = new bool[16];

        public void FillFrom(World world, int tick)
        {
            Tick = tick;
            var s = world?.Strikes;
            if (s == null)
            {
                StrikeCount = 0;
                FallCount = 0;
                FireCount = 0;
                return;
            }

            StrikeCount = s.StrikeCount;
            EnsureStrikeCapacity(StrikeCount);
            for (int i = 0; i < StrikeCount; i++)
            {
                StrikeXMm[i] = s.StrikeXMm(i);
                StrikeYMm[i] = s.StrikeYMm(i);
                StrikeTick[i] = s.StrikeTick(i);
                StrikeEvent[i] = s.StrikeEvent(i);
            }

            FallCount = s.FallCount;
            EnsureFallCapacity(FallCount);
            for (int i = 0; i < FallCount; i++)
            {
                FallTree[i] = s.FallTree(i);
                FallHeadingUnits[i] = s.FallHeading(i);
                FallTick[i] = s.FallTick(i);
                FallCause[i] = s.FallCause(i);
                FallLanded[i] = s.FallLanded(i);
            }

            FireCount = s.FireCount;
            EnsureFireCapacity(FireCount);
            for (int i = 0; i < FireCount; i++)
            {
                FireBlocker[i] = s.FireBlocker(i);
                FireEnds[i] = s.FireEnds(i);
                FireEvent[i] = s.FireEvent(i);
                FireOut[i] = s.FireOut(i);
            }
        }

        public void CopyTo(StrikeSnapshot target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            target.Tick = Tick;

            target.StrikeCount = StrikeCount;
            target.EnsureStrikeCapacity(StrikeCount);
            if (StrikeCount > 0)
            {
                Array.Copy(StrikeXMm, target.StrikeXMm, StrikeCount);
                Array.Copy(StrikeYMm, target.StrikeYMm, StrikeCount);
                Array.Copy(StrikeTick, target.StrikeTick, StrikeCount);
                Array.Copy(StrikeEvent, target.StrikeEvent, StrikeCount);
            }

            target.FallCount = FallCount;
            target.EnsureFallCapacity(FallCount);
            if (FallCount > 0)
            {
                Array.Copy(FallTree, target.FallTree, FallCount);
                Array.Copy(FallHeadingUnits, target.FallHeadingUnits, FallCount);
                Array.Copy(FallTick, target.FallTick, FallCount);
                Array.Copy(FallCause, target.FallCause, FallCount);
                Array.Copy(FallLanded, target.FallLanded, FallCount);
            }

            target.FireCount = FireCount;
            target.EnsureFireCapacity(FireCount);
            if (FireCount > 0)
            {
                Array.Copy(FireBlocker, target.FireBlocker, FireCount);
                Array.Copy(FireEnds, target.FireEnds, FireCount);
                Array.Copy(FireEvent, target.FireEvent, FireCount);
                Array.Copy(FireOut, target.FireOut, FireCount);
            }
        }

        private void EnsureStrikeCapacity(int required)
        {
            if (required <= StrikeXMm.Length)
            {
                return;
            }
            int cap = Math.Max(required, StrikeXMm.Length * 2);
            Array.Resize(ref StrikeXMm, cap);
            Array.Resize(ref StrikeYMm, cap);
            Array.Resize(ref StrikeTick, cap);
            Array.Resize(ref StrikeEvent, cap);
        }

        private void EnsureFallCapacity(int required)
        {
            if (required <= FallTree.Length)
            {
                return;
            }
            int cap = Math.Max(required, FallTree.Length * 2);
            Array.Resize(ref FallTree, cap);
            Array.Resize(ref FallHeadingUnits, cap);
            Array.Resize(ref FallTick, cap);
            Array.Resize(ref FallCause, cap);
            Array.Resize(ref FallLanded, cap);
        }

        private void EnsureFireCapacity(int required)
        {
            if (required <= FireBlocker.Length)
            {
                return;
            }
            int cap = Math.Max(required, FireBlocker.Length * 2);
            Array.Resize(ref FireBlocker, cap);
            Array.Resize(ref FireEnds, cap);
            Array.Resize(ref FireEvent, cap);
            Array.Resize(ref FireOut, cap);
        }
    }

    /// <summary>Snapshot of herd mood, fear, stampede headings, and drover control (<see cref="HerdStates"/>).</summary>
    public sealed class HerdSnapshot
    {
        public int Tick;
        public int Count;
        public int[] Regiment = new int[16];
        public HerdMode[] Mode = new HerdMode[16];
        public int[] Since = new int[16];
        public int[] Fear = new int[16];
        public int[] HeadingUnits = new int[16];
        public int[] DrivenBy = new int[16];
        public int[] EventIndex = new int[16];

        public void FillFrom(World world, int tick)
        {
            Tick = tick;
            var herds = world?.Herds;
            int n = herds != null ? herds.Count : 0;
            Count = n;
            EnsureCapacity(n);
            for (int h = 0; h < n; h++)
            {
                Regiment[h] = herds.Regiment[h];
                Mode[h] = herds.Mode[h];
                Since[h] = herds.Since[h];
                Fear[h] = herds.Fear[h];
                HeadingUnits[h] = herds.Heading[h];
                DrivenBy[h] = herds.DrivenBy[h];
                EventIndex[h] = herds.Event[h];
            }
        }

        public void CopyTo(HerdSnapshot target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            target.Tick = Tick;
            target.Count = Count;
            target.EnsureCapacity(Count);
            if (Count > 0)
            {
                Array.Copy(Regiment, target.Regiment, Count);
                Array.Copy(Mode, target.Mode, Count);
                Array.Copy(Since, target.Since, Count);
                Array.Copy(Fear, target.Fear, Count);
                Array.Copy(HeadingUnits, target.HeadingUnits, Count);
                Array.Copy(DrivenBy, target.DrivenBy, Count);
                Array.Copy(EventIndex, target.EventIndex, Count);
            }
        }

        private void EnsureCapacity(int required)
        {
            if (required <= Regiment.Length)
            {
                return;
            }
            int cap = Math.Max(required, Regiment.Length * 2);
            Array.Resize(ref Regiment, cap);
            Array.Resize(ref Mode, cap);
            Array.Resize(ref Since, cap);
            Array.Resize(ref Fear, cap);
            Array.Resize(ref HeadingUnits, cap);
            Array.Resize(ref DrivenBy, cap);
            Array.Resize(ref EventIndex, cap);
        }
    }
}
